using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Core.Services;

public class AppSettingsService : IAppSettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TrafficNovaPro", "settings.json");

    private readonly ILogger<AppSettingsService> _logger;

    public AppSettings Current { get; private set; } = new();

    public AppSettingsService(ILogger<AppSettingsService> logger)
    {
        _logger = logger;
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(SettingsPath))
        {
            Current = new AppSettings();
            await SaveAsync();
            _logger.LogInformation("Created default settings at {Path}", SettingsPath);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath);
            Current = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            _logger.LogInformation("Loaded settings from {Path}", SettingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings; using defaults");
            Current = new AppSettings();
        }
    }

    public async Task SaveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonConvert.SerializeObject(Current, Formatting.Indented);
        await File.WriteAllTextAsync(SettingsPath, json);
        _logger.LogDebug("Settings saved");
    }

    public void ResetToDefaults()
    {
        Current = new AppSettings();
    }
}
