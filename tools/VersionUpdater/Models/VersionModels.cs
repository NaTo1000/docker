using System.Text.Json.Serialization;

namespace VersionUpdater.Models;

/// <summary>Root model mirroring the structure of versions.json.</summary>
public sealed class VersionsRoot : Dictionary<string, VersionEntry?> { }

/// <summary>Per-major-version entry in versions.json.</summary>
public sealed class VersionEntry
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("arches")]
    public Dictionary<string, ArchEntry> Arches { get; set; } = new();

    [JsonPropertyName("dindCommit")]
    public string DindCommit { get; set; } = string.Empty;

    [JsonPropertyName("buildx")]
    public PluginInfo Buildx { get; set; } = new();

    [JsonPropertyName("compose")]
    public PluginInfo Compose { get; set; } = new();

    [JsonPropertyName("variants")]
    public List<string> Variants { get; set; } = new();
}

/// <summary>Architecture-specific download URLs for the main Docker binary.</summary>
public sealed class ArchEntry
{
    [JsonPropertyName("dockerUrl")]
    public string DockerUrl { get; set; } = string.Empty;

    [JsonPropertyName("rootlessExtrasUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RootlessExtrasUrl { get; set; }
}

/// <summary>Versioned plugin (buildx or compose) including per-arch download info.</summary>
public sealed class PluginInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("arches")]
    public Dictionary<string, PluginArchEntry> Arches { get; set; } = new();
}

/// <summary>Per-arch download metadata for a versioned plugin.</summary>
public sealed class PluginArchEntry
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
