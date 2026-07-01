using PawDesk.App.Services;
using System.IO;

namespace PawDesk.Tests;

public sealed class AppPathServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PawDesk.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureDirectories_CreatesExpectedDataFolders()
    {
        var paths = new AppPathService(_root);

        paths.EnsureDirectories();

        Assert.True(Directory.Exists(paths.RootDirectory));
        Assert.True(Directory.Exists(paths.OriginalPetsDirectory));
        Assert.True(Directory.Exists(paths.ProcessedPetsDirectory));
        Assert.True(Directory.Exists(paths.LogsDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
