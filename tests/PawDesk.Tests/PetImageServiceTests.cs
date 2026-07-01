using PawDesk.App.Services;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PawDesk.Tests;

public sealed class PetImageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PawDesk.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ImportImage_CopiesPngToOriginalAndProcessedFolders()
    {
        var sourcePath = Path.Combine(_root, "source.png");
        Directory.CreateDirectory(_root);
        SavePng(sourcePath, alpha: 0);
        var paths = new AppPathService(Path.Combine(_root, "data"));
        var service = new PetImageService(paths, new FakeBackgroundRemovalService());

        var result = await service.ImportImageAsync(sourcePath);

        Assert.True(result.Success);
        Assert.NotNull(result.ProcessedImagePath);
        Assert.True(File.Exists(result.ProcessedImagePath));
        Assert.Single(Directory.GetFiles(paths.OriginalPetsDirectory));
        Assert.Single(Directory.GetFiles(paths.ProcessedPetsDirectory));
    }

    [Fact]
    public async Task ImportImage_UsesBackgroundRemoval_ForNonTransparentImages()
    {
        var sourcePath = Path.Combine(_root, "source.jpg");
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(sourcePath, [1, 2, 3]);
        var paths = new AppPathService(Path.Combine(_root, "data"));
        var backgroundRemoval = new FakeBackgroundRemovalService();
        var service = new PetImageService(paths, backgroundRemoval);

        var result = await service.ImportImageAsync(sourcePath);

        Assert.True(result.Success);
        Assert.True(backgroundRemoval.WasCalled);
        Assert.Single(Directory.GetFiles(paths.OriginalPetsDirectory));
        Assert.Single(Directory.GetFiles(paths.ProcessedPetsDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void SavePng(string path, byte alpha)
    {
        var pixels = new byte[] { 0, 0, 255, alpha };
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private sealed class FakeBackgroundRemovalService : IBackgroundRemovalService
    {
        public bool WasCalled { get; private set; }

        public Task<BackgroundRemovalResult> RemoveBackgroundAsync(
            string inputImagePath,
            string outputDirectory,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, "fake-output.png");
            SavePng(outputPath, alpha: 255);
            return Task.FromResult(new BackgroundRemovalResult
            {
                Success = true,
                OutputImagePath = outputPath
            });
        }
    }
}
