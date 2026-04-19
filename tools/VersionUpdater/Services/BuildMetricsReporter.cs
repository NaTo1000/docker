using VersionUpdater.Models;

namespace VersionUpdater.Services;

/// <summary>
/// Writes a detailed, human-readable build-metrics report to the console,
/// including per-component timings and a dependency change summary.
/// </summary>
public static class BuildMetricsReporter
{
    private const string Separator = "────────────────────────────────────────────────────────";

    /// <summary>Prints the full metrics report to standard output.</summary>
    public static void Report(BuildMetrics m)
    {
        var c = Console.IsOutputRedirected ? NoColor : AnsiColors;

        Console.WriteLine();
        Console.WriteLine($"{c.Bold}{c.Cyan}╔══ ArchiTEK Dependency Update – Build Metrics ══╗{c.Reset}");
        Console.WriteLine(Separator);

        // ----- Timing breakdown -----------------------------------------------
        Console.WriteLine($"{c.Bold}⏱  Timing breakdown{c.Reset}");
        PrintTiming("dind commit fetch",    m.DindFetchDuration,          c);
        PrintTiming("Docker versions fetch", m.DockerVersionFetchDuration, c);
        PrintTiming("Buildx fetch",          m.BuildxFetchDuration,        c);
        PrintTiming("Compose fetch",         m.ComposeFetchDuration,       c);
        PrintTiming("Arch URL probes",        m.ArchProbesDuration,         c);
        PrintTiming("Total elapsed",          m.Elapsed,                    c, bold: true);
        Console.WriteLine(Separator);

        // ----- Dependency change summary --------------------------------------
        Console.WriteLine($"{c.Bold}📦  Dependency versions{c.Reset}");
        PrintDep("Docker",  m.PreviousDockerVersion,  m.NewDockerVersion,  c);
        PrintDep("Buildx",  m.PreviousBuildxVersion,  m.NewBuildxVersion,  c);
        PrintDep("Compose", m.PreviousComposeVersion, m.NewComposeVersion, c);
        PrintCommitDep("dind commit", m.PreviousDindCommit, m.NewDindCommit, c);
        Console.WriteLine(Separator);

        // ----- Architecture coverage ------------------------------------------
        Console.WriteLine($"{c.Bold}🏗️  Architecture coverage{c.Reset}");
        Console.WriteLine(
            $"  Probed   : {m.TotalArchsProbed}");
        Console.WriteLine(
            $"  {c.Green}Available{c.Reset} : {m.ArchsAvailable} – {string.Join(", ", m.AvailableArchList)}");
        if (m.SkippedArchList.Count > 0)
            Console.WriteLine(
                $"  {c.Yellow}Skipped{c.Reset}   : {m.ArchsSkipped} – {string.Join(", ", m.SkippedArchList)}");
        Console.WriteLine(Separator);

        // ----- HTTP request health --------------------------------------------
        Console.WriteLine($"{c.Bold}🌐  HTTP requests{c.Reset}");
        Console.WriteLine($"  Total   : {m.HttpRequestCount}");
        Console.WriteLine($"  {c.Green}Success{c.Reset} : {m.HttpSuccessCount}");
        if (m.HttpFailureCount > 0)
            Console.WriteLine($"  {c.Yellow}404/Err{c.Reset} : {m.HttpFailureCount}");
        Console.WriteLine(Separator);

        // ----- Version entry summary ------------------------------------------
        Console.WriteLine($"{c.Bold}📄  versions.json summary{c.Reset}");
        Console.WriteLine($"  Versions processed : {m.VersionsProcessed}");
        Console.WriteLine($"  Versions skipped   : {m.VersionsSkipped}");
        Console.WriteLine($"  Variants generated : {m.VariantsGenerated}");
        Console.WriteLine(Separator);

        // ----- Overall status -------------------------------------------------
        var statusColor = m.AnyDependencyChanged ? c.Green : c.Cyan;
        var statusText  = m.AnyDependencyChanged ? "UPDATED" : "UP-TO-DATE";
        Console.WriteLine(
            $"{c.Bold}✅  Status: {statusColor}{statusText}{c.Reset}{c.Bold} (run finished at {m.FinishedAt:u}){c.Reset}");
        Console.WriteLine();
    }

    private static void PrintTiming(
        string label, TimeSpan duration, ColorScheme c, bool bold = false)
    {
        var prefix = bold ? c.Bold : string.Empty;
        Console.WriteLine(
            $"  {prefix}{label,-25}{c.Reset}: {prefix}{duration.TotalSeconds,7:F2}s{c.Reset}");
    }

    private static void PrintDep(
        string name, string? prev, string? current, ColorScheme c)
    {
        if (current is null)
        {
            Console.WriteLine($"  {name,-8}: {c.Yellow}(not fetched){c.Reset}");
            return;
        }

        if (prev is null || prev == current)
        {
            Console.WriteLine($"  {name,-8}: {c.Cyan}{current}{c.Reset}  (unchanged)");
        }
        else
        {
            Console.WriteLine(
                $"  {name,-8}: {c.Yellow}{prev}{c.Reset} → {c.Green}{current}{c.Reset}  ★ updated");
        }
    }

    private static void PrintCommitDep(
        string name, string? prev, string? current, ColorScheme c)
    {
        if (current is null)
        {
            Console.WriteLine($"  {name}: {c.Yellow}(not fetched){c.Reset}");
            return;
        }

        string shortCurrent = current.Length >= 8 ? current[..8] : current;
        if (prev is null || prev == current)
        {
            Console.WriteLine($"  {name}: {c.Cyan}{shortCurrent}…{c.Reset}  (unchanged)");
        }
        else
        {
            string shortPrev = prev.Length >= 8 ? prev[..8] : prev;
            Console.WriteLine(
                $"  {name}: {c.Yellow}{shortPrev}…{c.Reset} → {c.Green}{shortCurrent}…{c.Reset}  ★ updated");
        }
    }

    // -------------------------------------------------------------------------
    // ANSI color helpers
    // -------------------------------------------------------------------------

    private sealed record ColorScheme(
        string Reset, string Bold, string Green, string Yellow, string Cyan);

    private static readonly ColorScheme AnsiColors = new(
        Reset:  "\x1b[0m",
        Bold:   "\x1b[1m",
        Green:  "\x1b[32m",
        Yellow: "\x1b[33m",
        Cyan:   "\x1b[36m");

    private static readonly ColorScheme NoColor = new(
        Reset:  string.Empty,
        Bold:   string.Empty,
        Green:  string.Empty,
        Yellow: string.Empty,
        Cyan:   string.Empty);
}
