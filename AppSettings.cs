using System.IO;
using System.Text.Json;

namespace StandReminder;

public class AppSettings
{
    public int SitMinutes { get; set; } = 45;
    public int StandMinutes { get; set; } = 15;
    public string WorkStart { get; set; } = "07:00";
    public string WorkEnd { get; set; } = "16:00";
    public bool PlaySound { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;

    public TimeSpan WorkStartTime => TimeSpan.TryParse(WorkStart, out var t) ? t : new TimeSpan(7, 0, 0);
    public TimeSpan WorkEndTime => TimeSpan.TryParse(WorkEnd, out var t) ? t : new TimeSpan(16, 0, 0);

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StandReminder");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* corrupted file -> fall back to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
