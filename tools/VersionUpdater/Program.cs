using System.Text.Json;
using System.Text.RegularExpressions;
using VersionUpdater.Models;
using VersionUpdater.Services;

// ---------------------------------------------------------------------------
// ArchiTEK Dependency Version Updater
// ---------------------------------------------------------------------------
// Rewrite in C# of the versions.sh shell script, with added build metrics.
//
// Usage:
//   dotnet run [-- <version> ...]
//
// With no arguments every version key in versions.json is refreshed.
// With explicit version keys (e.g. "29", "29-rc") only those are updated.
// ---------------------------------------------------------------------------

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// Resolve the repository root (two levels up from the tool project).
var repoRoot = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var versionsJsonPath = Path.Combine(repoRoot, "versions.json");

if (!File.Exists(versionsJsonPath))
{
    Console.Error.WriteLine(
        $"error: versions.json not found at '{versionsJsonPath}'. " +
        "Run this tool from the repository root or ensure the path is correct.");
    return 1;
}

// Determine which version keys to process.
var requestedVersions = args.Length > 0
    ? args.ToList()
    : null; // null → process all

Console.WriteLine($"ArchiTEK Dependency Version Updater  (repo: {repoRoot})");
Console.WriteLine($"versions.json  : {versionsJsonPath}");
Console.WriteLine(
    $"Versions scope : {(requestedVersions is null ? "all" : string.Join(", ", requestedVersions))}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Read existing versions.json to capture previous values for the diff report.
// ---------------------------------------------------------------------------
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented   = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    ReadCommentHandling  = JsonCommentHandling.Skip,
};

VersionsRoot existingRoot;
try
{
    var existingJson = await File.ReadAllTextAsync(versionsJsonPath, cts.Token);
    existingRoot = JsonSerializer.Deserialize<VersionsRoot>(existingJson, jsonOptions)
                  ?? new VersionsRoot();
}
catch (JsonException ex)
{
    Console.Error.WriteLine($"error: failed to parse versions.json – {ex.Message}");
    return 1;
}

// ---------------------------------------------------------------------------
// Fetch all upstream data.
// ---------------------------------------------------------------------------
var metrics = new BuildMetrics();

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
    "VersionUpdater/1.0 (docker-library/docker ArchiTEK)");
httpClient.Timeout = TimeSpan.FromMinutes(5);

var fetcher = new VersionFetcherService(httpClient, metrics);

Console.WriteLine("Fetching dind helper commit …");
string dindCommit = await fetcher.FetchDindCommitAsync(cts.Token);
Console.WriteLine($"  dind commit : {dindCommit[..8]}…");

Console.WriteLine("Fetching Docker engine versions …");
var dockerVersions = await fetcher.FetchDockerVersionsAsync(cts.Token);
Console.WriteLine($"  found {dockerVersions.Count} tags; latest: {dockerVersions[0]}");

Console.WriteLine("Fetching buildx release …");
var buildxInfo = await fetcher.FetchBuildxInfoAsync(cts.Token);
Console.WriteLine(
    $"  buildx {buildxInfo.Version} ({buildxInfo.Arches.Count} arches)");

Console.WriteLine("Fetching compose release …");
var composeInfo = await fetcher.FetchComposeInfoAsync(cts.Token);
Console.WriteLine(
    $"  compose {composeInfo.Version} ({composeInfo.Arches.Count} arches)");

// ---------------------------------------------------------------------------
// Determine which version keys exist in the current JSON.
// ---------------------------------------------------------------------------
var allVersionKeys = requestedVersions
    ?? existingRoot.Keys
        .Where(k => existingRoot[k] is not null)
        .ToList();

// Start with the existing content (so keys not being updated are preserved).
var outputRoot = new VersionsRoot();
foreach (var (k, v) in existingRoot)
    outputRoot[k] = v;

// Capture "before" state for metrics.
// Pick the first non-null entry as representative.
var firstExisting = existingRoot.Values.FirstOrDefault(e => e is not null);
metrics.PreviousDockerVersion  = firstExisting?.Version;
metrics.PreviousBuildxVersion  = firstExisting?.Buildx.Version;
metrics.PreviousComposeVersion = firstExisting?.Compose.Version;
metrics.PreviousDindCommit     = firstExisting?.DindCommit;

Console.WriteLine();

// ---------------------------------------------------------------------------
// Process each version key.
// ---------------------------------------------------------------------------
var preReleasePattern = new Regex(@"-(rc|tp|beta)\d*$", RegexOptions.IgnoreCase);

foreach (var versionKey in allVersionKeys)
{
    cts.Token.ThrowIfCancellationRequested();

    var rcVersion = versionKey.EndsWith("-rc", StringComparison.OrdinalIgnoreCase)
        ? versionKey[..^3]
        : versionKey;

    var channel = versionKey != rcVersion ? "test" : "stable";

    // Find matching docker version strings (matching the major.minor prefix).
    var versionOptions = dockerVersions
        .Where(v => v.StartsWith($"{rcVersion}.", StringComparison.OrdinalIgnoreCase))
        .ToList();

    // For stable tracks, exclude pre-release suffixes.
    string? fullVersion = versionKey == rcVersion
        ? versionOptions.FirstOrDefault(v => !preReleasePattern.IsMatch(v))
        : versionOptions.FirstOrDefault(v =>  preReleasePattern.IsMatch(v));

    if (fullVersion is null)
    {
        // Check if the version was already null (pre-announced but unreleased).
        if (existingRoot.TryGetValue(versionKey, out var existingValue)
            && existingValue is null)
        {
            Console.WriteLine(
                $"[{versionKey}] Skipping – not released yet (was null in versions.json)");
            outputRoot[versionKey] = null;
            metrics.VersionsSkipped++;
            continue;
        }

        Console.Error.WriteLine(
            $"error: cannot find full version for key '{versionKey}'");
        return 1;
    }

    // If this is a "-rc" key, skip it when the GA version is already equal or newer.
    if (versionKey != rcVersion
        && existingRoot.TryGetValue(rcVersion, out var gaEntry)
        && gaEntry is not null)
    {
        var latestVersion = new[] { fullVersion, gaEntry.Version }
            .OrderByDescending(v => v, new VersionStringComparer())
            .First();

        if (fullVersion.StartsWith(gaEntry.Version, StringComparison.Ordinal)
            || latestVersion == gaEntry.Version)
        {
            Console.WriteLine(
                $"[{versionKey}] Skipping – GA '{gaEntry.Version}' is newer than rc '{fullVersion}'");
            outputRoot[versionKey] = null;
            metrics.VersionsSkipped++;
            continue;
        }
    }

    Console.WriteLine(
        $"[{versionKey}] {fullVersion}  (buildx {buildxInfo.Version}, compose {composeInfo.Version})");

    var entry = await fetcher.BuildVersionEntryAsync(
        fullVersion, dindCommit, buildxInfo, composeInfo, channel, cts.Token);

    outputRoot[versionKey] = entry;
    // Keep symmetry: ensure both "XX.YY" and "XX.YY-rc" keys exist.
    outputRoot.TryAdd(rcVersion, null);
    outputRoot.TryAdd($"{rcVersion}-rc", null);

    metrics.VersionsProcessed++;

    Console.WriteLine(
        $"  arches available: {string.Join(", ", entry.Arches.Keys.OrderBy(x => x))}");
}

// ---------------------------------------------------------------------------
// Write updated versions.json (keys in sorted order, matching jq -S behaviour).
// ---------------------------------------------------------------------------
metrics.NewDockerVersion  = outputRoot.Values.FirstOrDefault(e => e is not null)?.Version;
metrics.NewBuildxVersion  = buildxInfo.Version;
metrics.NewComposeVersion = composeInfo.Version;
metrics.NewDindCommit     = dindCommit;

var sortedRoot = new VersionsRoot();
foreach (var key in outputRoot.Keys.OrderBy(k => k, StringComparer.Ordinal))
    sortedRoot[key] = outputRoot[key];

var outputJson = JsonSerializer.Serialize(sortedRoot, jsonOptions);
await File.WriteAllTextAsync(versionsJsonPath, outputJson + "\n", cts.Token);

Console.WriteLine();
Console.WriteLine($"versions.json written to: {versionsJsonPath}");

// ---------------------------------------------------------------------------
// Build metrics report.
// ---------------------------------------------------------------------------
metrics.MarkFinished();
BuildMetricsReporter.Report(metrics);

return 0;
