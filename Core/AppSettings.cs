using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StandReminder;

public class AppSettings
{
    public int SitMinutes { get; set; } = 45;
    public int StandMinutes { get; set; } = 15;
    public string WorkStart { get; set; } = "07:00";
    public string WorkEnd { get; set; } = "16:00";
    public bool PlaySound { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public string Language { get; set; } = "sk"; // "sk" | "en"

    // Auto-update (GitHub Releases)
    public bool AutoUpdateCheck { get; set; } = true;  // check on startup + once a day
    public string SkippedVersion { get; set; } = "";   // release tag the user chose to skip
    public string? LastUpdateCheck { get; set; } = null; // ISO timestamp, throttles to 1×/day
    public string PendingUpdateVersion { get; set; } = ""; // tag we are updating to; checked once on next start

    public TimeSpan WorkStartTime => TimeSpan.TryParse(WorkStart, out var t) ? t : new TimeSpan(7, 0, 0);
    public TimeSpan WorkEndTime => TimeSpan.TryParse(WorkEnd, out var t) ? t : new TimeSpan(16, 0, 0);

    [JsonIgnore]
    public DateTime? LastUpdateCheckTime =>
        DateTime.TryParse(LastUpdateCheck, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)
            ? t : null;

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
