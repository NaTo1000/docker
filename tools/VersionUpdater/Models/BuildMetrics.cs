namespace VersionUpdater.Models;

/// <summary>Captures timing and change information for a single update run.</summary>
public sealed class BuildMetrics
{
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; private set; }
    public TimeSpan Elapsed => (FinishedAt ?? DateTimeOffset.UtcNow) - StartedAt;

    // Per-component fetch timings
    public TimeSpan DindFetchDuration { get; set; }
    public TimeSpan DockerVersionFetchDuration { get; set; }
    public TimeSpan BuildxFetchDuration { get; set; }
    public TimeSpan ComposeFetchDuration { get; set; }
    public TimeSpan ArchProbesDuration { get; set; }

    // Version change summary
    public string? PreviousDockerVersion { get; set; }
    public string? NewDockerVersion { get; set; }
    public string? PreviousBuildxVersion { get; set; }
    public string? NewBuildxVersion { get; set; }
    public string? PreviousComposeVersion { get; set; }
    public string? NewComposeVersion { get; set; }
    public string? PreviousDindCommit { get; set; }
    public string? NewDindCommit { get; set; }

    // Architecture coverage
    public int TotalArchsProbed { get; set; }
    public int ArchsAvailable { get; set; }
    public int ArchsSkipped { get; set; }
    public List<string> AvailableArchList { get; set; } = new();
    public List<string> SkippedArchList { get; set; } = new();

    // HTTP health
    public int HttpRequestCount { get; set; }
    public int HttpSuccessCount { get; set; }
    public int HttpFailureCount { get; set; }

    // Version entry statistics
    public int VersionsProcessed { get; set; }
    public int VersionsSkipped { get; set; }
    public int VariantsGenerated { get; set; }

    // Derived helpers
    public bool DockerVersionChanged =>
        PreviousDockerVersion is not null && PreviousDockerVersion != NewDockerVersion;
    public bool BuildxVersionChanged =>
        PreviousBuildxVersion is not null && PreviousBuildxVersion != NewBuildxVersion;
    public bool ComposeVersionChanged =>
        PreviousComposeVersion is not null && PreviousComposeVersion != NewComposeVersion;
    public bool DindCommitChanged =>
        PreviousDindCommit is not null && PreviousDindCommit != NewDindCommit;
    public bool AnyDependencyChanged =>
        DockerVersionChanged || BuildxVersionChanged || ComposeVersionChanged || DindCommitChanged;

    public void MarkFinished() => FinishedAt = DateTimeOffset.UtcNow;
}
