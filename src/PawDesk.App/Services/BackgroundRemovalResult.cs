namespace PawDesk.App.Services;

public sealed class BackgroundRemovalResult
{
    public bool Success { get; init; }
    public string? OutputImagePath { get; init; }
    public string? ErrorMessage { get; init; }
}
