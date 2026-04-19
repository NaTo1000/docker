using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using VersionUpdater.Models;

namespace VersionUpdater.Services;

/// <summary>
/// Fetches latest Docker engine, buildx, and compose releases plus the dind
/// helper commit from GitHub, mirroring the logic in versions.sh.
/// </summary>
public sealed class VersionFetcherService : IDisposable
{
    // Map of bashbrew architecture names to Docker release architecture names.
    private static readonly IReadOnlyDictionary<string, string> DockerArches =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amd64"]        = "x86_64",
            ["arm32v6"]      = "armel",
            ["arm32v7"]      = "armhf",
            ["arm64v8"]      = "aarch64",
            ["ppc64le"]      = "ppc64le",
            ["riscv64"]      = "riscv64",
            ["s390x"]        = "s390x",
            ["windows-amd64"] = "x86_64",
        };

    // Architectures that support rootless extras.
    private static readonly IReadOnlySet<string> RootlessExtraArches =
        new HashSet<string>(StringComparer.Ordinal) { "amd64", "arm64v8" };

    // Architectures that must be present for the update to proceed.
    private static readonly IReadOnlySet<string> AlwaysRequiredArches =
        new HashSet<string>(StringComparer.Ordinal) { "amd64", "arm64v8" };

    private readonly HttpClient _httpClient;
    private readonly BuildMetrics _metrics;

    public VersionFetcherService(HttpClient httpClient, BuildMetrics metrics)
    {
        _httpClient = httpClient;
        _metrics = metrics;
    }

    // -------------------------------------------------------------------------
    // dind helper commit
    // -------------------------------------------------------------------------

    /// <summary>Returns the latest commit SHA for hack/dind in docker/docker.</summary>
    public async Task<string> FetchDindCommitAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // The Atom feed returns JSON under the 'payload' key when accessed with
            // Accept: application/json – same trick used in versions.sh.
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://github.com/docker/docker/commits/master/hack/dind.atom");
            request.Headers.Add("Accept", "application/json");

            var response = await SendWithMetricsAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            // Parse the OID from the first commit in the atom response payload.
            var match = Regex.Match(body, @"""oid""\s*:\s*""([0-9a-f]{40})""");
            if (!match.Success)
                throw new InvalidOperationException(
                    "Could not parse dind commit OID from GitHub Atom feed.");

            return match.Groups[1].Value;
        }
        finally
        {
            sw.Stop();
            _metrics.DindFetchDuration = sw.Elapsed;
        }
    }

    // -------------------------------------------------------------------------
    // Docker engine versions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all Docker engine version strings available as GitHub tags,
    /// ordered newest-first (mirrors the jq logic in versions.sh).
    /// </summary>
    public async Task<IReadOnlyList<string>> FetchDockerVersionsAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var tagLines = await RunGitLsRemoteAsync(
                "https://github.com/docker/docker.git", ct);

            var versions = tagLines
                .Select(l => Regex.Match(l,
                    @"refs/tags/(docker-)?v(?<ver>[0-9][0-9a-z.\-]+?)(\^\{\})?$"))
                .Where(m => m.Success)
                .Select(m => m.Groups["ver"].Value)
                .Distinct(StringComparer.Ordinal)
                // Sort semantically (numeric segments first, then pre-release tags)
                .OrderByDescending(v => v, new VersionStringComparer())
                .ToList();

            return versions;
        }
        finally
        {
            sw.Stop();
            _metrics.DockerVersionFetchDuration = sw.Elapsed;
        }
    }

    // -------------------------------------------------------------------------
    // Buildx
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fetches the latest buildx release that has a checksums.txt file and
    /// returns a populated <see cref="PluginInfo"/>.
    /// </summary>
    public async Task<PluginInfo> FetchBuildxInfoAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var tagLines = await RunGitLsRemoteAsync(
                "https://github.com/docker/buildx.git", ct);

            var versions = tagLines
                .Select(l => Regex.Match(l,
                    @"refs/tags/v(?<ver>[0-9][0-9.]+?)(\^\{\})?$"))
                .Where(m => m.Success)
                .Select(m => m.Groups["ver"].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(v => v, new VersionStringComparer())
                .ToList();

            foreach (var version in versions)
            {
                var checksumsUrl =
                    $"https://github.com/docker/buildx/releases/download/v{version}/checksums.txt";
                var body = await TryGetStringAsync(checksumsUrl, ct);
                if (body is null)
                    continue;

                var arches = ParseBuildxChecksums(body, version);
                if (arches.Count == 0)
                    continue;

                return new PluginInfo { Version = version, Arches = arches };
            }

            throw new InvalidOperationException("Failed to determine buildx version.");
        }
        finally
        {
            sw.Stop();
            _metrics.BuildxFetchDuration = sw.Elapsed;
        }
    }

    private static Dictionary<string, PluginArchEntry> ParseBuildxChecksums(
        string checksums, string version)
    {
        // Map from buildx release architecture suffix to bashbrew arch name.
        var archMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["linux-amd64"]        = "amd64",
            ["linux-arm-v6"]       = "arm32v6",
            ["linux-arm-v7"]       = "arm32v7",
            ["linux-arm64"]        = "arm64v8",
            ["linux-ppc64le"]      = "ppc64le",
            ["linux-riscv64"]      = "riscv64",
            ["linux-s390x"]        = "s390x",
            ["freebsd-amd64"]      = "freebsd-amd64",
            ["freebsd-arm64"]      = "freebsd-arm64v8",
            ["netbsd-amd64"]       = "netbsd-amd64",
            ["netbsd-arm64"]       = "netbsd-arm64v8",
            ["openbsd-amd64"]      = "openbsd-amd64",
            ["openbsd-arm64"]      = "openbsd-arm64v8",
            ["windows-amd64"]      = "windows-amd64",
            ["windows-arm64"]      = "windows-arm64v8",
        };

        var result = new Dictionary<string, PluginArchEntry>(StringComparer.Ordinal);

        foreach (var line in checksums.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Each line: "<sha256>  <filename>"
            var parts = line.Split(new[] { ' ', '\t' }, 2,
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                continue;

            var sha256 = parts[0].Trim();
            var file   = parts[1].Trim();

            if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            // Match pattern: buildx-vX.Y.Z.<os>-<arch>[.exe]
            var m = Regex.Match(file,
                @"^buildx-v[\d.]+[.](?<os>linux|windows|darwin|freebsd|openbsd|netbsd)-(?<arch>[^.]+)(?<ext>[.]exe)?$");
            if (!m.Success)
                continue;

            var osName    = m.Groups["os"].Value;
            var archName  = m.Groups["arch"].Value;
            var ext       = m.Groups["ext"].Value;
            var osArchKey = $"{osName}-{archName}";

            if (!archMap.TryGetValue(osArchKey, out var bashbrewArch))
                continue;

            result[bashbrewArch] = new PluginArchEntry
            {
                File   = file,
                Sha256 = sha256,
                Url    = $"https://github.com/docker/buildx/releases/download/v{version}/{file}",
            };
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Compose
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fetches the latest compose release that has a checksums.txt file and
    /// returns a populated <see cref="PluginInfo"/>.
    /// </summary>
    public async Task<PluginInfo> FetchComposeInfoAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var tagLines = await RunGitLsRemoteAsync(
                "https://github.com/docker/compose.git", ct);

            var versions = tagLines
                .Select(l => Regex.Match(l,
                    @"refs/tags/v(?<ver>[0-9][0-9.]+?)(\^\{\})?$"))
                .Where(m => m.Success)
                .Select(m => m.Groups["ver"].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(v => v, new VersionStringComparer())
                .ToList();

            foreach (var version in versions)
            {
                var checksumsUrl =
                    $"https://github.com/docker/compose/releases/download/v{version}/checksums.txt";
                var body = await TryGetStringAsync(checksumsUrl, ct);
                if (body is null)
                    continue;

                var arches = ParseComposeChecksums(body, version);
                if (arches.Count == 0)
                    continue;

                return new PluginInfo { Version = version, Arches = arches };
            }

            throw new InvalidOperationException("Failed to determine compose version.");
        }
        finally
        {
            sw.Stop();
            _metrics.ComposeFetchDuration = sw.Elapsed;
        }
    }

    private static Dictionary<string, PluginArchEntry> ParseComposeChecksums(
        string checksums, string version)
    {
        var archMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["linux-x86_64"]    = "amd64",
            ["linux-armv6"]     = "arm32v6",
            ["linux-armv7"]     = "arm32v7",
            ["linux-aarch64"]   = "arm64v8",
            ["linux-ppc64le"]   = "ppc64le",
            ["linux-riscv64"]   = "riscv64",
            ["linux-s390x"]     = "s390x",
            ["darwin-x86_64"]   = "darwin-amd64",
            ["darwin-aarch64"]  = "darwin-arm64v8",
            ["windows-x86_64"]  = "windows-amd64",
            ["windows-aarch64"] = "windows-arm64v8",
        };

        var result = new Dictionary<string, PluginArchEntry>(StringComparer.Ordinal);

        foreach (var line in checksums.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(new[] { ' ', '\t' }, 2,
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                continue;

            var sha256 = parts[0].Trim();
            var file   = parts[1].Trim();

            if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            // Pattern: docker-compose-<os>-<arch>[.exe]
            var m = Regex.Match(file,
                @"^docker-compose-(?<os>linux|windows|darwin|freebsd|openbsd)-(?<arch>[^.]+)(?<ext>[.]exe)?$");
            if (!m.Success)
                continue;

            var osName   = m.Groups["os"].Value;
            var archName = m.Groups["arch"].Value;
            var osArchKey = $"{osName}-{archName}";

            if (!archMap.TryGetValue(osArchKey, out var bashbrewArch))
                continue;

            result[bashbrewArch] = new PluginArchEntry
            {
                File   = file,
                Sha256 = sha256,
                Url    = $"https://github.com/docker/compose/releases/download/v{version}/{file}",
            };
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Docker arch availability probing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Probes Docker download servers for each architecture to see which
    /// binaries are actually available for <paramref name="fullVersion"/>.
    /// </summary>
    public async Task<VersionEntry> BuildVersionEntryAsync(
        string fullVersion,
        string dindCommit,
        PluginInfo buildx,
        PluginInfo compose,
        string channel,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var entry = new VersionEntry
        {
            Version    = fullVersion,
            DindCommit = dindCommit,
            Buildx     = buildx,
            Compose    = compose,
        };

        _metrics.TotalArchsProbed = DockerArches.Count;
        var available  = new List<string>();
        var skipped    = new List<string>();

        // Probe each architecture in parallel for speed.
        var probeTasks = DockerArches
            .Select(kv => ProbeArchAsync(kv.Key, kv.Value, fullVersion, channel, entry, ct))
            .ToList();

        var results = await Task.WhenAll(probeTasks);
        foreach (var (bashbrewArch, found) in results)
        {
            if (found) available.Add(bashbrewArch);
            else        skipped.Add(bashbrewArch);
        }

        _metrics.ArchsAvailable = available.Count;
        _metrics.ArchsSkipped   = skipped.Count;
        _metrics.AvailableArchList = available.OrderBy(x => x).ToList();
        _metrics.SkippedArchList   = skipped.OrderBy(x => x).ToList();

        // Verify that mandatory architectures are present.
        foreach (var required in AlwaysRequiredArches)
        {
            if (!available.Contains(required))
                throw new InvalidOperationException(
                    $"Required architecture '{required}' is not available for Docker {fullVersion}. " +
                    "This usually indicates a scraping fluke – aborting.");
        }

        // Build variant list (same order as versions.sh).
        entry.Variants = new List<string>
        {
            "cli",
            "dind",
            "dind-rootless",
            "windows/windowsservercore-ltsc2025",
            "windows/windowsservercore-ltsc2022",
        };
        _metrics.VariantsGenerated += entry.Variants.Count;

        sw.Stop();
        _metrics.ArchProbesDuration = sw.Elapsed;
        return entry;
    }

    private async Task<(string Arch, bool Found)> ProbeArchAsync(
        string bashbrewArch,
        string dockerReleaseArch,
        string fullVersion,
        string channel,
        VersionEntry entry,
        CancellationToken ct)
    {
        bool isWindows = bashbrewArch.StartsWith("windows-", StringComparison.Ordinal);
        string url = isWindows
            ? $"https://download.docker.com/win/static/{channel}/{dockerReleaseArch}/docker-{fullVersion}.zip"
            : $"https://download.docker.com/linux/static/{channel}/{dockerReleaseArch}/docker-{fullVersion}.tgz";

        bool available = await HeadRequestSucceedsAsync(url, ct);
        if (!available)
            return (bashbrewArch, false);

        var archEntry = new ArchEntry { DockerUrl = url };

        if (!isWindows && RootlessExtraArches.Contains(bashbrewArch))
        {
            var rootlessUrl =
                $"https://download.docker.com/linux/static/{channel}/{dockerReleaseArch}/docker-rootless-extras-{fullVersion}.tgz";
            if (await HeadRequestSucceedsAsync(rootlessUrl, ct))
                archEntry.RootlessExtrasUrl = rootlessUrl;
        }

        // Thread-safe insertion.
        lock (entry.Arches)
        {
            entry.Arches[bashbrewArch] = archEntry;
        }

        return (bashbrewArch, true);
    }

    // -------------------------------------------------------------------------
    // Low-level helpers
    // -------------------------------------------------------------------------

    /// <summary>Runs git ls-remote and returns all output lines.</summary>
    private static async Task<IReadOnlyList<string>> RunGitLsRemoteAsync(
        string repoUrl, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git")
        {
            Arguments              = $"ls-remote --tags {repoUrl}",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var err = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException(
                $"git ls-remote failed for {repoUrl}: {err}");
        }

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private async Task<bool> HeadRequestSucceedsAsync(string url, CancellationToken ct)
    {
        try
        {
            using var request  = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);
            _metrics.HttpRequestCount++;
            if (response.IsSuccessStatusCode)
            {
                _metrics.HttpSuccessCount++;
                return true;
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _metrics.HttpFailureCount++;
                return false;
            }
            throw new HttpRequestException(
                $"Unexpected HTTP {(int)response.StatusCode} for HEAD {url}");
        }
        catch (HttpRequestException ex) when (
            ex.Message.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
        {
            _metrics.HttpRequestCount++;
            _metrics.HttpFailureCount++;
            return false;
        }
    }

    private async Task<string?> TryGetStringAsync(string url, CancellationToken ct)
    {
        try
        {
            _metrics.HttpRequestCount++;
            var response = await _httpClient.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _metrics.HttpFailureCount++;
                return null;
            }
            response.EnsureSuccessStatusCode();
            _metrics.HttpSuccessCount++;
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException)
        {
            _metrics.HttpFailureCount++;
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendWithMetricsAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        _metrics.HttpRequestCount++;
        var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            _metrics.HttpSuccessCount++;
        else
            _metrics.HttpFailureCount++;
        return response;
    }

    public void Dispose() => _httpClient.Dispose();
}
