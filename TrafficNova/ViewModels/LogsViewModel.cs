using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrafficNova.Core.Interfaces;

namespace TrafficNova.ViewModels;

public record LogEntry(DateTime Timestamp, string Level, string Message);

public partial class LogsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settings;
    private readonly DispatcherTimer _timer;

    [ObservableProperty] private ObservableCollection<LogEntry> _entries = new();
    [ObservableProperty] private string _filterLevel = "All";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _autoRefresh = true;

    public string[] LevelOptions { get; } = ["All", "Information", "Warning", "Error", "Fatal"];

    public LogsViewModel(IAppSettingsService settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => { if (AutoRefresh) LoadLogs(); };
        LoadLogs();
    }

    public void StartRefreshTimer() => _timer.Start();
    public void StopRefreshTimer()  => _timer.Stop();

    [RelayCommand]
    private void LoadLogs()
    {
        var logDir = _settings.Current.LogDirectory;
        if (!Directory.Exists(logDir)) return;

        var today = DateTime.Now.ToString("yyyyMMdd");
        var logFile = Directory.GetFiles(logDir, $"app-{today}*.log")
            .OrderByDescending(f => f).FirstOrDefault()
            ?? Directory.GetFiles(logDir, "app-*.log")
               .OrderByDescending(f => f).FirstOrDefault();

        if (logFile is null) return;

        try
        {
            var lines = ReadLogFileLines(logFile);
            var parsed = lines.Select(ParseLine)
                .Where(e => e is not null)
                .Cast<LogEntry>()
                .Where(e => FilterLevel == "All" || e.Level == FilterLevel)
                .Where(e => string.IsNullOrWhiteSpace(SearchText)
                    || e.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .TakeLast(500)
                .Reverse()
                .ToList();

            Entries = new ObservableCollection<LogEntry>(parsed);
        }
        catch { /* log file may be locked */ }
    }

    // Read only the tail of the log file. The daily log can grow to 50 MB and
    // this runs on a 5-second timer — reading the whole file every tick would
    // be O(file size) of disk + parsing work. 256 KB holds far more than the
    // 500 lines the view keeps.
    private static IEnumerable<string> ReadLogFileLines(string path)
    {
        const long TailBytes = 256 * 1024;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        bool truncated = stream.Length > TailBytes;
        if (truncated)
            stream.Seek(-TailBytes, SeekOrigin.End);
        using var reader = new StreamReader(stream);
        if (truncated)
            reader.ReadLine(); // discard the partial line at the seek point
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line is not null) yield return line;
        }
    }

    private static LogEntry? ParseLine(string line)
    {
        // Serilog default format: "2026-05-18 10:00:00.000 +00:00 [INF] Message"
        if (line.Length < 30) return null;
        try
        {
            var levelStart = line.IndexOf('[');
            var levelEnd = line.IndexOf(']', levelStart + 1);
            if (levelStart < 0 || levelEnd < 0) return null;

            var timestamp = DateTime.TryParse(line[..levelStart].Trim(), out var dt) ? dt : DateTime.Now;
            var levelCode = line[(levelStart + 1)..levelEnd];
            var level = levelCode switch
            {
                "VRB" or "DBG" => "Debug",
                "INF" => "Information",
                "WRN" => "Warning",
                "ERR" => "Error",
                "FTL" => "Fatal",
                _ => levelCode
            };
            var message = line[(levelEnd + 1)..].Trim();
            return new LogEntry(timestamp, level, message);
        }
        catch { return null; }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        Entries.Clear();
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var dir = _settings.Current.LogDirectory;
        if (Directory.Exists(dir))
            System.Diagnostics.Process.Start("explorer.exe", dir);
    }
}
