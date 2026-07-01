using System.IO;

namespace PawDesk.App.Services;

public sealed class PetImageService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    private readonly AppPathService _paths;
    private readonly IBackgroundRemovalService _backgroundRemovalService;

    public PetImageService(AppPathService paths, IBackgroundRemovalService backgroundRemovalService)
    {
        _paths = paths;
        _backgroundRemovalService = backgroundRemovalService;
    }

    public async Task<PetImageResult> ImportImageAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _paths.EnsureDirectories();

            if (!File.Exists(sourcePath))
            {
                return new PetImageResult { Success = false, ErrorMessage = "图片文件不存在。" };
            }

            var extension = Path.GetExtension(sourcePath);
            if (!SupportedExtensions.Contains(extension))
            {
                return new PetImageResult { Success = false, ErrorMessage = "暂不支持这个图片格式。" };
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var originalPath = Path.Combine(_paths.OriginalPetsDirectory, $"pet-{timestamp}{extension.ToLowerInvariant()}");
            File.Copy(sourcePath, originalPath, overwrite: false);

            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                OnnxBackgroundRemovalService.HasTransparentPixels(originalPath))
            {
                var directProcessedPath = Path.Combine(_paths.ProcessedPetsDirectory, $"pet-{timestamp}.png");
                File.Copy(originalPath, directProcessedPath, overwrite: false);

                return new PetImageResult
                {
                    Success = true,
                    ProcessedImagePath = directProcessedPath
                };
            }

            var removalResult = await _backgroundRemovalService.RemoveBackgroundAsync(
                originalPath,
                _paths.ProcessedPetsDirectory,
                cancellationToken);

            if (!removalResult.Success || string.IsNullOrWhiteSpace(removalResult.OutputImagePath))
            {
                return new PetImageResult
                {
                    Success = false,
                    ErrorMessage = removalResult.ErrorMessage ?? "自动抠图失败。"
                };
            }

            return new PetImageResult
            {
                Success = true,
                ProcessedImagePath = removalResult.OutputImagePath
            };
        }
        catch (IOException)
        {
            return new PetImageResult { Success = false, ErrorMessage = "图片复制失败，请检查文件权限或磁盘空间。" };
        }
        catch (UnauthorizedAccessException)
        {
            return new PetImageResult { Success = false, ErrorMessage = "图片复制失败，请检查文件权限或磁盘空间。" };
        }
    }
}
