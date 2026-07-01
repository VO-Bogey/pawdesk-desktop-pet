using PawDesk.App.Models;
using PawDesk.App.Services;
using System.IO;

namespace PawDesk.Tests;

public sealed class AppSettingsServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PawDesk.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_CreatesDefaultSettings_WhenFileDoesNotExist()
    {
        var paths = new AppPathService(_root);
        var service = new AppSettingsService(paths);

        var settings = service.Load();

        Assert.True(File.Exists(paths.SettingsPath));
        Assert.Equal(1.0, settings.PetScale);
        Assert.True(settings.AnimationEnabled);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsSettings()
    {
        var paths = new AppPathService(_root);
        var service = new AppSettingsService(paths);
        var settings = new AppSettings
        {
            PetX = 123,
            PetY = 456,
            PetScale = 1.7,
            AnimationEnabled = false,
            AlwaysOnTop = false
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(123, loaded.PetX);
        Assert.Equal(456, loaded.PetY);
        Assert.Equal(1.7, loaded.PetScale);
        Assert.False(loaded.AnimationEnabled);
        Assert.False(loaded.AlwaysOnTop);
    }

    [Fact]
    public void Load_BacksUpBrokenSettings_AndRestoresDefaults()
    {
        var paths = new AppPathService(_root);
        paths.EnsureDirectories();
        File.WriteAllText(paths.SettingsPath, "{not valid json");
        var service = new AppSettingsService(paths);

        var settings = service.Load();

        Assert.Equal(1.0, settings.PetScale);
        Assert.True(File.Exists(paths.SettingsPath));
        Assert.Single(Directory.GetFiles(paths.RootDirectory, "settings.json.broken-*.bak"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
