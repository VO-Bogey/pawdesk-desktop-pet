using System;
using System.IO;

namespace PawDesk.App.Services;

public sealed class AppPathService
{
    public AppPathService(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PawDesk");
        PetsDirectory = Path.Combine(RootDirectory, "pets");
        OriginalPetsDirectory = Path.Combine(PetsDirectory, "original");
        ProcessedPetsDirectory = Path.Combine(PetsDirectory, "processed");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        SettingsPath = Path.Combine(RootDirectory, "settings.json");
    }

    public string RootDirectory { get; }
    public string PetsDirectory { get; }
    public string OriginalPetsDirectory { get; }
    public string ProcessedPetsDirectory { get; }
    public string LogsDirectory { get; }
    public string SettingsPath { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(OriginalPetsDirectory);
        Directory.CreateDirectory(ProcessedPetsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
