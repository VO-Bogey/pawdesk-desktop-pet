namespace PawDesk.App.Services;

public sealed class PetImageResult
{
    public bool Success { get; init; }
    public string? ProcessedImagePath { get; init; }
    public string? ErrorMessage { get; init; }
}
