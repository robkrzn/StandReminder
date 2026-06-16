using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace StandReminder;

/// <summary>Thrown when a downloaded update fails integrity verification (checksum mismatch).</summary>
public class UpdateVerificationException : Exception
{
    public UpdateVerificationException(string message) : base(message) { }
}

/// <summary>
/// Downloads the standalone release zip, extracts it, then hands off to a detached
/// PowerShell updater that waits for this process to exit, overwrites the running
/// exe (which Windows keeps locked while we run) and relaunches the new version.
/// The caller is expected to <c>Shutdown()</c> right after this returns.
/// </summary>
public static class UpdateInstaller
{
    private static string TempRoot =>
        Path.Combine(Path.GetTempPath(), "StandReminderUpdate");

    /// <summary>
    /// Best-effort removal of any leftover update workspace (downloaded zip, extracted
    /// files, updater script). Called on startup so the freshly-installed build tidies up
    /// after itself — the updater script can't delete itself while it is still running.
    /// </summary>
    public static void CleanupTemp()
    {
        try
        {
            if (Directory.Exists(TempRoot))
                Directory.Delete(TempRoot, recursive: true);
        }
        catch { /* still in use / no rights — will be retried on the next startup */ }
    }

    /// <summary>
    /// Download + extract + spawn updater. Reports download progress (0..1) when the
    /// server sends a content length. Throws on failure (caller shows an error and
    /// keeps the app running).
    /// </summary>
    public static async Task DownloadAndApplyAsync(UpdateInfo info, IProgress<double>? progress = null)
    {
        if (string.IsNullOrEmpty(info.DownloadUrl))
            throw new InvalidOperationException("No downloadable asset for this release.");

        // Fresh workspace each run — also cleans whatever a previous update left behind.
        CleanupTemp();
        Directory.CreateDirectory(TempRoot);

        string zipPath = Path.Combine(TempRoot, "update.zip");
        string extractDir = Path.Combine(TempRoot, "extracted");

        await DownloadAsync(info.DownloadUrl!, zipPath, progress).ConfigureAwait(false);

        // integrity check: the downloaded bytes must match the hash GitHub reported.
        // Catches corrupted/truncated downloads and CDN-level tampering before we run anything.
        await VerifyChecksumAsync(zipPath, info.Sha256).ConfigureAwait(false);

        ZipFile.ExtractToDirectory(zipPath, extractDir);

        // the zip is no longer needed once extracted — free the ~66 MB right away
        try { File.Delete(zipPath); } catch { /* leftover handled by startup CleanupTemp */ }

        string newExe = Directory
            .EnumerateFiles(extractDir, "StandReminder.exe", SearchOption.AllDirectories)
            .FirstOrDefault()
            ?? throw new FileNotFoundException("StandReminder.exe not found in the update package.");

        string targetExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine the running executable path.");

        string scriptPath = Path.Combine(TempRoot, "StandReminder.update.ps1");
        File.WriteAllText(scriptPath, UpdaterScript);

        LaunchUpdater(scriptPath, Environment.ProcessId, newExe, targetExe, extractDir);
    }

    private static async Task DownloadAsync(string url, string destPath, IProgress<double>? progress)
    {
        // defense in depth: never fetch from anything but an HTTPS GitHub host
        if (!UpdateChecker.IsTrustedDownloadHost(url))
            throw new UpdateVerificationException($"Refusing to download from an untrusted host: {url}");

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StandReminder", "1.0"));

        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        long? total = resp.Content.Headers.ContentLength;
        using var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var dst = File.Create(destPath);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await src.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
            received += read;
            if (total is > 0)
                progress?.Report((double)received / total.Value);
        }
        progress?.Report(1.0);
    }

    private static async Task VerifyChecksumAsync(string path, string? expectedSha256)
    {
        // older releases may not carry a digest — nothing to compare against, so don't block
        if (string.IsNullOrEmpty(expectedSha256)) return;

        string actual;
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
            actual = Convert.ToHexString(await sha.ComputeHashAsync(stream).ConfigureAwait(false));

        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new UpdateVerificationException(
                $"Checksum mismatch — expected {expectedSha256}, got {actual}. Update aborted.");
    }

    private static void LaunchUpdater(string scriptPath, int pid, string newExe,
                                      string targetExe, string cleanupDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath()
        };
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-WindowStyle");
        psi.ArgumentList.Add("Hidden");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("-ProcessId"); psi.ArgumentList.Add(pid.ToString());
        psi.ArgumentList.Add("-NewExe"); psi.ArgumentList.Add(newExe);
        psi.ArgumentList.Add("-TargetExe"); psi.ArgumentList.Add(targetExe);
        psi.ArgumentList.Add("-CleanupDir"); psi.ArgumentList.Add(cleanupDir);

        Process.Start(psi);
    }

    /// <summary>
    /// Detached updater: wait for the old process to exit (releasing the exe lock),
    /// copy the new exe over it with a few retries, relaunch, then tidy up temp files.
    /// </summary>
    private const string UpdaterScript = """
        param(
            [int]$ProcessId,
            [string]$NewExe,
            [string]$TargetExe,
            [string]$CleanupDir
        )

        try { Wait-Process -Id $ProcessId -Timeout 30 -ErrorAction SilentlyContinue } catch {}
        Start-Sleep -Milliseconds 800

        $copied = $false
        for ($i = 0; $i -lt 10; $i++) {
            try {
                Copy-Item -LiteralPath $NewExe -Destination $TargetExe -Force -ErrorAction Stop
                $copied = $true
                break
            } catch {
                Start-Sleep -Milliseconds 500
            }
        }

        Start-Process -FilePath $TargetExe

        # extracted payload is no longer needed once copied; the updater script itself and
        # its folder are wiped by the new app on startup (it can't delete itself mid-run)
        if ($copied) {
            try { Remove-Item -LiteralPath $CleanupDir -Recurse -Force -ErrorAction SilentlyContinue } catch {}
        }
        """;
}
