using System;
using Microsoft.Win32;

namespace PawDesk.App.Services;

public sealed class StartupService
{
    private const string AppName = "PawDesk";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true) ??
                        Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                return;
            }

            key.SetValue(AppName, $"\"{exePath}\"");
            return;
        }

        key.DeleteValue(AppName, throwOnMissingValue: false);
    }
}
