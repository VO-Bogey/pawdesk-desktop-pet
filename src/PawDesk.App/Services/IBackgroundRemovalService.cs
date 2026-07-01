namespace PawDesk.App.Services;

public interface IBackgroundRemovalService
{
    Task<BackgroundRemovalResult> RemoveBackgroundAsync(
        string inputImagePath,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
