using System.Text.Json;

namespace Muxi;

internal static class VersionCheck
{
    private const string SdkName = "csharp";
    private static readonly TimeSpan TwelveHours = TimeSpan.FromHours(12);
    private static bool _checked = false;
    private static readonly object _lock = new();

    public static void CheckForUpdates(HttpResponseMessage response)
    {
        lock (_lock)
        {
            if (_checked) return;
            _checked = true;
        }

        if (!IsDevMode()) return;

        if (!response.Headers.TryGetValues("X-Muxi-SDK-Latest", out var values)) return;
        var latest = values.FirstOrDefault();
        if (string.IsNullOrEmpty(latest)) return;

        if (!IsNewerVersion(latest, MuxiVersion.Version)) return;

        UpdateLatestVersion(latest);

        if (!NotifiedRecently())
        {
            Console.Error.WriteLine($"[muxi] SDK update available: {latest} (current: {MuxiVersion.Version})");
            Console.Error.WriteLine("[muxi] Update via NuGet: dotnet add package Muxi");
            MarkNotified();
        }
    }

    private static bool IsDevMode() => Environment.GetEnvironmentVariable("MUXI_DEBUG") == "1";

    private static string? GetCachePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) return null;
        return Path.Combine(home, ".muxi", "sdk-versions.json");
    }

    private static Dictionary<string, VersionEntry> LoadCache()
    {
        try
        {
            var path = GetCachePath();
            if (path == null || !File.Exists(path)) return new();
            var content = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, VersionEntry>>(content) ?? new();
        }
        catch { return new(); }
    }

    private static void SaveCache(Dictionary<string, VersionEntry> cache)
    {
        try
        {
            var path = GetCachePath();
            if (path == null) return;
            var dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var content = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, content);
        }
        catch { }
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        try { return new Version(latest) > new Version(current); }
        catch { return string.Compare(latest, current, StringComparison.Ordinal) > 0; }
    }

    private static bool NotifiedRecently()
    {
        try
        {
            var cache = LoadCache();
            if (!cache.TryGetValue(SdkName, out var entry) || entry.LastNotified == null) return false;
            return DateTime.UtcNow - DateTime.Parse(entry.LastNotified) < TwelveHours;
        }
        catch { return false; }
    }

    private static void UpdateLatestVersion(string latest)
    {
        var cache = LoadCache();
        if (!cache.TryGetValue(SdkName, out var entry)) entry = new();
        entry.Current = MuxiVersion.Version;
        entry.Latest = latest;
        cache[SdkName] = entry;
        SaveCache(cache);
    }

    private static void MarkNotified()
    {
        var cache = LoadCache();
        if (cache.TryGetValue(SdkName, out var entry))
        {
            entry.LastNotified = DateTime.UtcNow.ToString("O");
            SaveCache(cache);
        }
    }

    private class VersionEntry
    {
        public string? Current { get; set; }
        public string? Latest { get; set; }
        public string? LastNotified { get; set; }
    }
}
