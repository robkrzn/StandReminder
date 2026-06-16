using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace StandReminder;

/// <summary>Outcome of an update check (used to drive UX, esp. manual checks).</summary>
public enum UpdateCheckStatus
{
    UpToDate,        // latest release is not newer than us
    UpdateAvailable, // newer release found and not skipped
    Skipped,         // newer release found but its tag matches the skipped version
    Failed           // no internet / API error / parse error
}

/// <summary>A newer release worth offering to the user.</summary>
public record UpdateInfo(Version Version, string TagName, string Notes, string? DownloadUrl, string HtmlUrl, string? Sha256);

public record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Info);

/// <summary>
/// Checks GitHub Releases for a newer StandReminder build. Read-only, no NuGet:
/// <c>HttpClient</c> + <c>System.Text.Json</c>. Any failure degrades to
/// <see cref="UpdateCheckStatus.Failed"/> — the app never crashes or nags over it.
/// </summary>
public static class UpdateChecker
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/robkrzn/StandReminder/releases/latest";

    // The standalone (self-contained) asset always works regardless of installed
    // runtime, so we always update to it — see doc/FEATURE-auto-update.md.
    private const string AssetSuffix = "-standalone.zip";

    /// <param name="current">This build's assembly version (e.g. 1.0.3.0).</param>
    /// <param name="skippedTag">Tag the user chose to skip; pass null for manual checks.</param>
    public static async Task<UpdateCheckResult> CheckAsync(Version current, string? skippedTag)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StandReminder", current.ToString()));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var stream = await http.GetStreamAsync(LatestReleaseUrl).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            var root = doc.RootElement;

            string tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!TryParseTag(tag, out var latest))
                return new UpdateCheckResult(UpdateCheckStatus.Failed, null);

            if (Normalize(latest) <= Normalize(current))
                return new UpdateCheckResult(UpdateCheckStatus.UpToDate, null);

            string notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
            var (download, sha256) = FindStandaloneAsset(root);

            var info = new UpdateInfo(latest, tag, notes.Trim(), download, htmlUrl, sha256);

            if (!string.IsNullOrEmpty(skippedTag) &&
                string.Equals(skippedTag, tag, StringComparison.OrdinalIgnoreCase))
                return new UpdateCheckResult(UpdateCheckStatus.Skipped, info);

            return new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, info);
        }
        catch
        {
            // offline, rate-limited, malformed JSON, … — stay silent, app keeps working
            return new UpdateCheckResult(UpdateCheckStatus.Failed, null);
        }
    }

    /// <summary>"v1.0.4" / "1.0.4" → Version. Returns false for non-version tags.</summary>
    private static bool TryParseTag(string tag, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        string s = tag.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
        return Version.TryParse(s, out version!);
    }

    /// <summary>Treat unset Build/Revision (-1) as 0 so 1.0.4 &gt; 1.0.3.0 compares correctly.</summary>
    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    /// <summary>
    /// Find the standalone asset's download URL and its server-side SHA-256 digest.
    /// Rejects the asset if its URL is not an HTTPS GitHub host (so a forged release
    /// can't point the updater at an arbitrary download server).
    /// </summary>
    private static (string? url, string? sha256) FindStandaloneAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return (null, null);

        foreach (var asset in assets.EnumerateArray())
        {
            string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (!name.EndsWith(AssetSuffix, StringComparison.OrdinalIgnoreCase)) continue;

            string? url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
            if (url == null || !IsTrustedDownloadHost(url))
                return (null, null); // suspicious host → offer no in-app update (page link only)

            // GitHub exposes the asset hash as "sha256:<hex>" (may be absent on old releases)
            string? sha = null;
            if (asset.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String)
            {
                string raw = d.GetString() ?? "";
                if (raw.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    sha = raw["sha256:".Length..];
            }
            return (url, sha);
        }
        return (null, null);
    }

    /// <summary>Only HTTPS URLs on GitHub's own hosts are allowed for downloads.</summary>
    public static bool IsTrustedDownloadHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps) return false;
        string host = uri.Host.ToLowerInvariant();
        return host == "github.com"
            || host == "www.github.com"
            || host.EndsWith(".githubusercontent.com", StringComparison.Ordinal);
    }
}
