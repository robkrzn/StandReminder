using System.IO;
using System.Text;

namespace StandReminder;

/// <summary>
/// Crash logging + health check. Writes to %APPDATA%\StandReminder\crash.log.
/// A running.flag file detects unclean shutdowns (crash, kill, power loss):
/// it is created on startup and deleted only on a clean exit.
/// </summary>
internal static class CrashLog
{
    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StandReminder");

    private static string LogPath => Path.Combine(Dir, "crash.log");
    private static string BackupPath => Path.Combine(Dir, "crash.old.log");
    private static string RunningFlag => Path.Combine(Dir, "running.flag");

    /// <summary>Rotation threshold per file → disk usage is capped at ~1 MB (log + backup).</summary>
    private const long MaxLogBytes = 512 * 1024;

    private static readonly object Gate = new();
    private static string? _lastEntry;
    private static DateTime _lastEntryAt;
    private static int _suppressed;

    public static void Install(System.Windows.Application app)
    {
        Directory.CreateDirectory(Dir);

        if (File.Exists(RunningFlag))
            Write("WARN", "Zistené nečisté ukončenie predchádzajúceho behu – aplikácia neskončila cez Ukončiť " +
                          "(pád, kill alebo výpadok). Pozri ERROR/FATAL záznamy vyššie.");
        try { File.WriteAllText(RunningFlag, DateTime.Now.ToString("o")); } catch { }

        // UI thread exceptions: log them and keep the app alive
        app.DispatcherUnhandledException += (_, e) =>
        {
            Write("ERROR", "Neošetrená výnimka na UI vlákne (aplikácia pokračuje)", e.Exception);
            e.Handled = true;
        };

        // anything escaping other threads is fatal – at least record it
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write("FATAL", "Neošetrená výnimka – proces končí", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("ERROR", "Nepozorovaná výnimka z Tasku", e.Exception);
            e.SetObserved();
        };

        Write("INFO", $"Aplikácia spustená (v{typeof(CrashLog).Assembly.GetName().Version}, PID {Environment.ProcessId})");
    }

    public static void MarkCleanExit()
    {
        try { File.Delete(RunningFlag); } catch { }
        Write("INFO", "Čisté ukončenie");
    }

    public static void Write(string level, string message, Exception? ex = null)
    {
        try
        {
            lock (Gate)
            {
                // an exception repeating in the 1 s timer tick would otherwise
                // append one entry per second — collapse repeats to one line a minute
                string entry = $"{level}|{message}|{ex?.GetType().Name}";
                var now = DateTime.Now;
                if (entry == _lastEntry && now - _lastEntryAt < TimeSpan.FromMinutes(1))
                {
                    _suppressed++;
                    return;
                }

                var sb = new StringBuilder();
                if (_suppressed > 0)
                {
                    sb.AppendLine($"[{now:yyyy-MM-dd HH:mm:ss}] INFO  Predchádzajúci záznam sa opakoval ešte {_suppressed}×");
                    _suppressed = 0;
                }
                _lastEntry = entry;
                _lastEntryAt = now;

                sb.AppendLine($"[{now:yyyy-MM-dd HH:mm:ss}] {level,-5} {message}");
                if (ex != null) sb.AppendLine(ex.ToString());

                RotateIfNeeded();
                File.AppendAllText(LogPath, sb.ToString());
            }
        }
        catch { /* logging must never take the app down */ }
    }

    /// <summary>When the log exceeds the limit, it becomes the backup (replacing the
    /// previous one) and a fresh file starts — recent history is always kept.</summary>
    private static void RotateIfNeeded()
    {
        var info = new FileInfo(LogPath);
        if (!info.Exists || info.Length < MaxLogBytes) return;
        File.Move(LogPath, BackupPath, overwrite: true);
    }
}
