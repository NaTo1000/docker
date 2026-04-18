using System.Text.RegularExpressions;

namespace VersionUpdater.Services;

/// <summary>
/// Compares Docker / buildx / compose version strings semantically.
/// Numeric segments are compared as integers; pre-release labels (rc, beta, tp)
/// sort before their GA counterparts.
/// </summary>
internal sealed class VersionStringComparer : IComparer<string>
{
    private static readonly Regex PreRelease =
        new(@"-(rc|beta|tp|alpha)\d*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public int Compare(string? x, string? y)
    {
        if (x is null && y is null) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var xParts = ParseVersion(x);
        var yParts = ParseVersion(y);

        int common = Math.Min(xParts.segments.Length, yParts.segments.Length);
        for (int i = 0; i < common; i++)
        {
            int cmp = xParts.segments[i].CompareTo(yParts.segments[i]);
            if (cmp != 0) return cmp;
        }

        if (xParts.segments.Length != yParts.segments.Length)
            return xParts.segments.Length.CompareTo(yParts.segments.Length);

        // Equal numeric prefix – stable (GA) sorts after pre-release.
        return yParts.isPreRelease.CompareTo(xParts.isPreRelease);
    }

    private static (int[] segments, bool isPreRelease) ParseVersion(string v)
    {
        bool isPreRelease = PreRelease.IsMatch(v);
        var clean = PreRelease.Replace(v, string.Empty);

        var segments = clean
            .Split('.')
            .Select(p =>
            {
                // Strip any trailing alphabetic suffix before parsing.
                var digits = Regex.Match(p, @"^\d+").Value;
                return int.TryParse(digits, out int n) ? n : 0;
            })
            .ToArray();

        return (segments, isPreRelease);
    }
}
