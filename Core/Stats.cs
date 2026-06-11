using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StandReminder;

public class DayStats
{
    public int SitSeconds { get; set; }
    public int StandSeconds { get; set; }
}

/// <summary>
/// Daily sit/stand totals persisted to %APPDATA%\StandReminder\stats.json.
/// History is capped to the last 7 calendar days — older entries are pruned
/// on every load and save, so the file can never grow.
/// </summary>
public class Stats
{
    public Dictionary<string, DayStats> Days { get; set; } = new(); // key: "yyyy-MM-dd"

    public const int KeepDays = 7;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StandReminder", "stats.json");

    public static Stats Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var stats = JsonSerializer.Deserialize<Stats>(File.ReadAllText(FilePath)) ?? new Stats();
                stats.Prune();
                return stats;
            }
        }
        catch { /* corrupted file -> start fresh */ }
        return new Stats();
    }

    public void Save()
    {
        try
        {
            Prune();
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* stats must never take the app down */ }
    }

    public void Add(Phase phase, int seconds)
    {
        string key = DateTime.Now.ToString("yyyy-MM-dd");
        if (!Days.TryGetValue(key, out var day))
            Days[key] = day = new DayStats();

        if (phase == Phase.Sitting) day.SitSeconds += seconds;
        else if (phase == Phase.Standing) day.StandSeconds += seconds;
    }

    [JsonIgnore]
    public DayStats Today =>
        Days.TryGetValue(DateTime.Now.ToString("yyyy-MM-dd"), out var day) ? day : new DayStats();

    /// <summary>Last 7 calendar days, oldest first; days without data come back as zeros.</summary>
    public List<(DateTime Date, DayStats Day)> LastWeek()
    {
        var week = new List<(DateTime, DayStats)>();
        for (int i = KeepDays - 1; i >= 0; i--)
        {
            var date = DateTime.Today.AddDays(-i);
            Days.TryGetValue(date.ToString("yyyy-MM-dd"), out var day);
            week.Add((date, day ?? new DayStats()));
        }
        return week;
    }

    private void Prune()
    {
        string cutoff = DateTime.Today.AddDays(-(KeepDays - 1)).ToString("yyyy-MM-dd");
        foreach (var key in Days.Keys.Where(k => string.CompareOrdinal(k, cutoff) < 0).ToList())
            Days.Remove(key);
    }
}
