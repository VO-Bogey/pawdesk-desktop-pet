using System;
using System.IO;
using System.Text.Json;
using PawDesk.App.Models;

namespace PawDesk.App.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppPathService _paths;

    public AppSettingsService(AppPathService paths)
    {
        _paths = paths;
    }

    public AppSettings Load()
    {
        _paths.EnsureDirectories();

        if (!File.Exists(_paths.SettingsPath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_paths.SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            BackupBrokenSettings();
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        _paths.EnsureDirectories();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_paths.SettingsPath, json);
    }

    private void BackupBrokenSettings()
    {
        if (!File.Exists(_paths.SettingsPath))
        {
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupPath = $"{_paths.SettingsPath}.broken-{timestamp}.bak";
        File.Move(_paths.SettingsPath, backupPath, overwrite: true);
    }
}
