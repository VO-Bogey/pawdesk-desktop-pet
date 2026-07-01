using System;
using System.IO;

namespace PawDesk.App.Services;

public sealed class LogService
{
    private readonly AppPathService _paths;

    public LogService(AppPathService paths)
    {
        _paths = paths;
    }

    public void Error(Exception exception, string message)
    {
        try
        {
            _paths.EnsureDirectories();
            var logPath = Path.Combine(_paths.LogsDirectory, $"{DateTime.Now:yyyyMMdd}.log");
            var content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(logPath, content);
        }
        catch
        {
            // Logging must never crash the desktop pet.
        }
    }
}
