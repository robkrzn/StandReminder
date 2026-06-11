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
    private static string RunningFlag => Path.Combine(Dir, "running.flag");

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
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level,-5} {message}");
            if (ex != null) sb.AppendLine(ex.ToString());
            File.AppendAllText(LogPath, sb.ToString());
        }
        catch { /* logging must never take the app down */ }
    }
}
