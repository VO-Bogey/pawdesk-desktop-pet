using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PawDesk.App.Services;

public sealed class OnnxBackgroundRemovalService : IBackgroundRemovalService, IDisposable
{
    private const int ModelSize = 320;
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] Std = [0.229f, 0.224f, 0.225f];
    private readonly Lazy<InferenceSession> _session;
    private readonly LogService _logService;
    private readonly string _modelPath;

    public OnnxBackgroundRemovalService(LogService logService, string? modelPath = null)
    {
        _logService = logService;
        _modelPath = modelPath ?? ResolveBundledModelPath();
        _session = new Lazy<InferenceSession>(() =>
        {
            return new InferenceSession(_modelPath);
        });
    }

    private static string ResolveBundledModelPath()
    {
        var appDataModelPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PawDesk",
            "models",
            "silueta.onnx");

        if (File.Exists(appDataModelPath))
        {
            return appDataModelPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(appDataModelPath)!);
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("silueta.onnx", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new FileNotFoundException("Bundled background-removal model was not found.");
        }

        using var resource = assembly.GetManifestResourceStream(resourceName) ??
                             throw new FileNotFoundException("Bundled background-removal model could not be opened.");
        using var output = File.Create(appDataModelPath);
        resource.CopyTo(output);
        return appDataModelPath;
    }

    public Task<BackgroundRemovalResult> RemoveBackgroundAsync(
        string inputImagePath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(outputDirectory);

                var image = LoadImage(inputImagePath);
                var input = BuildInputTensor(image);
                var inputName = _session.Value.InputMetadata.Keys.First();
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputName, input)
                };

                using var results = _session.Value.Run(inputs);
                var maskTensor = results.First().AsTensor<float>();
                var alphaMask = NormalizeMask(maskTensor);
                var outputPixels = ApplyAlphaMask(image, alphaMask);
                var outputPath = Path.Combine(outputDirectory, $"pet-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                SavePng(outputPath, image.Width, image.Height, outputPixels);

                return new BackgroundRemovalResult
                {
                    Success = true,
                    OutputImagePath = outputPath
                };
            }
            catch (OperationCanceledException)
            {
                return new BackgroundRemovalResult { Success = false, ErrorMessage = "已取消生成宠物。" };
            }
            catch (Exception exception)
            {
                _logService.Error(exception, "Background removal failed.");
                return new BackgroundRemovalResult { Success = false, ErrorMessage = "自动抠图失败，请换一张更清晰的图片或使用透明 PNG。" };
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_session.IsValueCreated)
        {
            _session.Value.Dispose();
        }
    }

    public static bool HasTransparentPixels(string imagePath)
    {
        try
        {
            var image = LoadImage(imagePath);
            for (var i = 3; i < image.Pixels.Length; i += 4)
            {
                if (image.Pixels[i] < 250)
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static ImageData LoadImage(string imagePath)
    {
        using var stream = File.OpenRead(imagePath);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return new ImageData(converted.PixelWidth, converted.PixelHeight, pixels);
    }

    private static DenseTensor<float> BuildInputTensor(ImageData image)
    {
        var tensor = new DenseTensor<float>([1, 3, ModelSize, ModelSize]);
        for (var y = 0; y < ModelSize; y++)
        {
            var srcY = Math.Clamp((int)Math.Round((double)y / (ModelSize - 1) * (image.Height - 1)), 0, image.Height - 1);
            for (var x = 0; x < ModelSize; x++)
            {
                var srcX = Math.Clamp((int)Math.Round((double)x / (ModelSize - 1) * (image.Width - 1)), 0, image.Width - 1);
                var srcIndex = (srcY * image.Width + srcX) * 4;
                var b = image.Pixels[srcIndex] / 255f;
                var g = image.Pixels[srcIndex + 1] / 255f;
                var r = image.Pixels[srcIndex + 2] / 255f;

                tensor[0, 0, y, x] = (r - Mean[0]) / Std[0];
                tensor[0, 1, y, x] = (g - Mean[1]) / Std[1];
                tensor[0, 2, y, x] = (b - Mean[2]) / Std[2];
            }
        }

        return tensor;
    }

    private static float[] NormalizeMask(Tensor<float> maskTensor)
    {
        var mask = new float[ModelSize * ModelSize];
        var min = float.MaxValue;
        var max = float.MinValue;

        for (var y = 0; y < ModelSize; y++)
        {
            for (var x = 0; x < ModelSize; x++)
            {
                var value = maskTensor[0, 0, y, x];
                mask[y * ModelSize + x] = value;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }
        }

        var range = max - min;
        if (range <= 0.00001f)
        {
            return mask.Select(_ => 0f).ToArray();
        }

        for (var i = 0; i < mask.Length; i++)
        {
            mask[i] = Math.Clamp((mask[i] - min) / range, 0f, 1f);
        }

        return mask;
    }

    private static byte[] ApplyAlphaMask(ImageData image, float[] mask)
    {
        var output = new byte[image.Pixels.Length];
        for (var y = 0; y < image.Height; y++)
        {
            var maskY = Math.Clamp((int)Math.Round((double)y / Math.Max(1, image.Height - 1) * (ModelSize - 1)), 0, ModelSize - 1);
            for (var x = 0; x < image.Width; x++)
            {
                var maskX = Math.Clamp((int)Math.Round((double)x / Math.Max(1, image.Width - 1) * (ModelSize - 1)), 0, ModelSize - 1);
                var srcIndex = (y * image.Width + x) * 4;
                var alpha = (byte)Math.Clamp((int)Math.Round(mask[maskY * ModelSize + maskX] * 255), 0, 255);

                output[srcIndex] = image.Pixels[srcIndex];
                output[srcIndex + 1] = image.Pixels[srcIndex + 1];
                output[srcIndex + 2] = image.Pixels[srcIndex + 2];
                output[srcIndex + 3] = alpha;
            }
        }

        return output;
    }

    private static void SavePng(string outputPath, int width, int height, byte[] bgraPixels)
    {
        var stride = width * 4;
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgraPixels, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(outputPath);
        encoder.Save(output);
    }

    private sealed record ImageData(int Width, int Height, byte[] Pixels);
}
