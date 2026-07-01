using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PawDesk.App.Models;
using PawDesk.App.Services;

namespace PawDesk.App;

public partial class PetWindow : Window
{
    private const double BaseSize = 220;
    private const double MinScale = 0.3;
    private const double MaxScale = 3.0;
    private readonly AppSettings _settings;
    private readonly AppSettingsService _settingsService;
    private readonly PetImageService _petImageService;
    private readonly StartupService _startupService;
    private readonly Action _openSettings;
    private readonly DispatcherTimer _swayTimer;
    private readonly DispatcherTimer _mouseTimer;
    private readonly DispatcherTimer _randomMoveTimer;
    private readonly Random _random = new();
    private System.Windows.Point _mouseDownScreenPoint;
    private BitmapSource? _uploadedBaseBitmap;
    private byte[]? _uploadedBasePixels;
    private Int32Rect? _uploadedForegroundBounds;
    private System.Windows.Point _uploadedHeadCenter = new(BaseSize / 2, BaseSize * 0.32);
    private bool _dragCompleted;
    private bool _isDragMoveActive;

    public PetWindow(
        AppSettings settings,
        AppSettingsService settingsService,
        PetImageService petImageService,
        StartupService startupService,
        Action openSettings)
    {
        InitializeComponent();

        _settings = settings;
        _settingsService = settingsService;
        _petImageService = petImageService;
        _startupService = startupService;
        _openSettings = openSettings;

        _swayTimer = new DispatcherTimer();
        _swayTimer.Tick += (_, _) => PlayRandomSway();

        _mouseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _mouseTimer.Tick += (_, _) => CheckMouseReaction();

        _randomMoveTimer = new DispatcherTimer();
        _randomMoveTimer.Tick += (_, _) => PlayRandomMove();

        ApplySettings();
    }

    private void ApplySettings()
    {
        Left = _settings.PetX;
        Top = _settings.PetY;
        Topmost = _settings.AlwaysOnTop;
        TopmostMenuItem.IsChecked = _settings.AlwaysOnTop;
        AnimationMenuItem.IsChecked = _settings.AnimationEnabled;
        StartupMenuItem.IsChecked = _startupService.IsEnabled();
        ApplyScale();
        LoadPetImage();

        if (_settings.AnimationEnabled)
        {
            StartAnimations();
        }
    }

    private void ApplyScale()
    {
        _settings.PetScale = Math.Clamp(_settings.PetScale, MinScale, MaxScale);
        Width = BaseSize * _settings.PetScale;
        Height = BaseSize * _settings.PetScale;
    }

    private void StartAnimations()
    {
        if (_settings.IdleBreathingEnabled)
        {
            StartBreathing();
        }

        ScheduleNextSway();
        ScheduleNextRandomMove();
        _mouseTimer.Start();
    }

    private void StartBreathing()
    {
        var storyboard = new Storyboard
        {
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true
        };

        var scaleX = CreateBreathingAnimation();
        Storyboard.SetTarget(scaleX, BreathScale);
        Storyboard.SetTargetProperty(scaleX, new PropertyPath("ScaleX"));
        storyboard.Children.Add(scaleX);

        var scaleY = CreateBreathingAnimation();
        Storyboard.SetTarget(scaleY, BreathScale);
        Storyboard.SetTargetProperty(scaleY, new PropertyPath("ScaleY"));
        storyboard.Children.Add(scaleY);

        PetRoot.BeginStoryboard(storyboard, HandoffBehavior.SnapshotAndReplace, true);
    }

    private static DoubleAnimation CreateBreathingAnimation()
    {
        return new DoubleAnimation
        {
            From = 1.0,
            To = 1.025,
            Duration = TimeSpan.FromMilliseconds(1100),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
    }

    private void StopAnimations()
    {
        _swayTimer.Stop();
        _mouseTimer.Stop();
        _randomMoveTimer.Stop();
        PetRoot.BeginStoryboard(null);
        BreathScale.ScaleX = 1;
        BreathScale.ScaleY = 1;
        SwayRotate.Angle = 0;
        LeanRotate.Angle = 0;
        ReactionTranslate.X = 0;
        ReactionTranslate.Y = 0;
        HeadRotate.Angle = 0;
        HeadTranslate.X = 0;
        HeadTranslate.Y = 0;
        BounceTranslate.Y = 0;
        if (_uploadedBaseBitmap is not null)
        {
            PetImage.Source = _uploadedBaseBitmap;
        }
    }

    private void ScheduleNextSway()
    {
        _swayTimer.Interval = TimeSpan.FromSeconds(_random.Next(8, 21));
        _swayTimer.Start();
    }

    private void ScheduleNextRandomMove()
    {
        _randomMoveTimer.Interval = TimeSpan.FromSeconds(_random.Next(20, 61));
        _randomMoveTimer.Start();
    }

    private void PlayRandomSway()
    {
        _swayTimer.Stop();
        if (!_settings.AnimationEnabled || _isDragMoveActive)
        {
            ScheduleNextSway();
            return;
        }

        var animation = new DoubleAnimationUsingKeyFrames();
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420))));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(640))));
        SwayRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, animation);
        ScheduleNextSway();
    }

    private void CheckMouseReaction()
    {
        if (!_settings.AnimationEnabled || !_settings.MouseReactionEnabled || _isDragMoveActive)
        {
            return;
        }

        var mouse = System.Windows.Forms.Control.MousePosition;
        var centerX = Left + Width / 2;
        var centerY = Top + Height / 2;
        var dx = mouse.X - centerX;
        var dy = mouse.Y - centerY;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance > 180 || distance < 1)
        {
            AnimateMouseReaction(0, 0, 0, 0);
            return;
        }

        var headX = Math.Clamp(dx / distance * 12, -12, 12);
        var headY = Math.Clamp(dy / distance * 8, -8, 8);
        var headAngle = Math.Clamp(dx / 180 * 12, -12, 12);
        var bodyAngle = Math.Clamp(dx / 180 * 4, -4, 4);
        AnimateMouseReaction(headX, headY, headAngle, bodyAngle);
    }

    private void AnimateMouseReaction(double headX, double headY, double headAngle, double bodyAngle)
    {
        var duration = TimeSpan.FromMilliseconds(140);
        var imageIsVisible = PetImage.Visibility == Visibility.Visible;
        if (imageIsVisible)
        {
            AnimateTo(HeadTranslate, System.Windows.Media.TranslateTransform.XProperty, 0, duration);
            AnimateTo(HeadTranslate, System.Windows.Media.TranslateTransform.YProperty, 0, duration);
            AnimateTo(HeadRotate, System.Windows.Media.RotateTransform.AngleProperty, 0, duration);
            AnimateTo(LeanRotate, System.Windows.Media.RotateTransform.AngleProperty, 0, duration);
            AnimateTo(ReactionTranslate, System.Windows.Media.TranslateTransform.XProperty, 0, duration);
            AnimateTo(ReactionTranslate, System.Windows.Media.TranslateTransform.YProperty, 0, duration);
            ApplyUploadedWarp(headX, headY * 0.7, headAngle);
            return;
        }

        AnimateTo(HeadTranslate, System.Windows.Media.TranslateTransform.XProperty, headX, duration);
        AnimateTo(HeadTranslate, System.Windows.Media.TranslateTransform.YProperty, headY, duration);
        AnimateTo(HeadRotate, System.Windows.Media.RotateTransform.AngleProperty, headAngle, duration);
        AnimateTo(LeanRotate, System.Windows.Media.RotateTransform.AngleProperty, imageIsVisible ? bodyAngle : 0, duration);
        AnimateTo(ReactionTranslate, System.Windows.Media.TranslateTransform.XProperty, imageIsVisible ? headX * 0.35 : 0, duration);
        AnimateTo(ReactionTranslate, System.Windows.Media.TranslateTransform.YProperty, imageIsVisible ? headY * 0.25 : 0, duration);
    }

    private static void AnimateTo(System.Windows.Media.Animation.Animatable target, DependencyProperty property, double value, TimeSpan duration)
    {
        var animation = new DoubleAnimation(value, duration)
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        };
        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void PlayRandomMove()
    {
        _randomMoveTimer.Stop();
        if (!_settings.AnimationEnabled || !_settings.RandomMoveEnabled || _isDragMoveActive)
        {
            ScheduleNextRandomMove();
            return;
        }

        var dx = _random.Next(-80, 81);
        var dy = _random.Next(-60, 61);
        var workArea = SystemParameters.WorkArea;
        Left = Math.Clamp(Left + dx, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
        Top = Math.Clamp(Top + dy, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));
        SavePosition();
        ScheduleNextRandomMove();
    }

    private void PlayClickBounce()
    {
        if (!_settings.AnimationEnabled || !_settings.ClickBounceEnabled)
        {
            return;
        }

        var bounce = new DoubleAnimationUsingKeyFrames();
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(-20, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)))
        {
            EasingFunction = new BounceEase { Bounces = 1, Bounciness = 2, EasingMode = EasingMode.EaseOut }
        });

        BounceTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, bounce);
    }

    private void LoadPetImage()
    {
        if (string.IsNullOrWhiteSpace(_settings.CurrentPetImagePath) ||
            !File.Exists(_settings.CurrentPetImagePath))
        {
            PetImage.Source = null;
            _uploadedBaseBitmap = null;
            _uploadedBasePixels = null;
            _uploadedForegroundBounds = null;
            PetImage.Visibility = Visibility.Collapsed;
            DefaultPet.Visibility = Visibility.Visible;
            return;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(_settings.CurrentPetImagePath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();

        PrepareUploadedWarpSource(image);
        PetImage.Source = _uploadedBaseBitmap ?? image;
        PetImage.Visibility = Visibility.Visible;
        DefaultPet.Visibility = Visibility.Collapsed;
    }

    private void PrepareUploadedWarpSource(BitmapSource source)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var scale = Math.Min(BaseSize / source.PixelWidth, BaseSize / source.PixelHeight);
            var width = source.PixelWidth * scale;
            var height = source.PixelHeight * scale;
            var x = (BaseSize - width) / 2;
            var y = (BaseSize - height) / 2;
            context.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, new Rect(0, 0, BaseSize, BaseSize));
            context.DrawImage(source, new Rect(x, y, width, height));
        }

        var render = new RenderTargetBitmap((int)BaseSize, (int)BaseSize, 96, 96, PixelFormats.Pbgra32);
        render.Render(visual);
        var converted = new FormatConvertedBitmap(render, PixelFormats.Bgra32, null, 0);
        converted.Freeze();

        var stride = (int)BaseSize * 4;
        var pixels = new byte[stride * (int)BaseSize];
        converted.CopyPixels(pixels, stride, 0);

        _uploadedBaseBitmap = BitmapSource.Create((int)BaseSize, (int)BaseSize, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        _uploadedBaseBitmap.Freeze();
        _uploadedBasePixels = pixels;
        _uploadedForegroundBounds = DetectForegroundBounds(pixels, (int)BaseSize, (int)BaseSize, stride);
        _uploadedHeadCenter = EstimateHeadCenter(pixels, (int)BaseSize, (int)BaseSize, stride, _uploadedForegroundBounds);
    }

    private static Int32Rect? DetectForegroundBounds(byte[] pixels, int width, int height, int stride)
    {
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var alpha = pixels[y * stride + x * 4 + 3];
                if (alpha < 32)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX < minX || maxY < minY
            ? null
            : new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static System.Windows.Point EstimateHeadCenter(byte[] pixels, int width, int height, int stride, Int32Rect? bounds)
    {
        if (bounds is null)
        {
            return new System.Windows.Point(BaseSize / 2, BaseSize * 0.32);
        }

        var rect = bounds.Value;
        var topLimit = rect.Y + (int)Math.Round(rect.Height * 0.55);
        var centerX = rect.X + rect.Width / 2.0;
        var leftWeight = 0.0;
        var rightWeight = 0.0;
        var weightedX = 0.0;
        var weightedY = 0.0;
        var total = 0.0;

        for (var y = rect.Y; y < Math.Min(height, topLimit); y++)
        {
            var verticalBias = 1.0 - Math.Clamp((y - rect.Y) / Math.Max(1.0, rect.Height * 0.55), 0, 1);
            verticalBias = 0.35 + verticalBias * verticalBias;

            for (var x = rect.X; x < Math.Min(width, rect.X + rect.Width); x++)
            {
                var alpha = pixels[y * stride + x * 4 + 3];
                if (alpha < 48)
                {
                    continue;
                }

                var sideBias = Math.Abs(x - centerX) / Math.Max(1.0, rect.Width / 2.0);
                var weight = alpha / 255.0 * verticalBias * (0.55 + sideBias * 0.65);
                weightedX += x * weight;
                weightedY += y * weight;
                total += weight;

                if (x < centerX)
                {
                    leftWeight += weight;
                }
                else
                {
                    rightWeight += weight;
                }
            }
        }

        if (total <= 0.001)
        {
            return new System.Windows.Point(rect.X + rect.Width * 0.52, rect.Y + rect.Height * 0.28);
        }

        var estimatedX = weightedX / total;
        var estimatedY = weightedY / total;
        var sideNudge = (rightWeight >= leftWeight ? 1 : -1) * rect.Width * 0.12;

        return new System.Windows.Point(
            Math.Clamp(estimatedX + sideNudge, rect.X + rect.Width * 0.15, rect.X + rect.Width * 0.85),
            Math.Clamp(estimatedY, rect.Y + rect.Height * 0.10, rect.Y + rect.Height * 0.45));
    }

    private void ApplyUploadedWarp(double pullX, double pullY, double angleDegrees)
    {
        if (_uploadedBaseBitmap is null || _uploadedBasePixels is null)
        {
            return;
        }

        if (Math.Abs(pullX) < 0.2 && Math.Abs(pullY) < 0.2 && Math.Abs(angleDegrees) < 0.2)
        {
            PetImage.Source = _uploadedBaseBitmap;
            return;
        }

        const int size = (int)BaseSize;
        const int stride = size * 4;
        var output = new byte[_uploadedBasePixels.Length];
        var bounds = _uploadedForegroundBounds ?? new Int32Rect(0, 0, size, size);
        var theta = angleDegrees * Math.PI / 180.0;
        var sigmaX = Math.Clamp(bounds.Width * 0.24, 22, 46);
        var sigmaY = Math.Clamp(bounds.Height * 0.22, 20, 42);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - _uploadedHeadCenter.X;
                var dy = y - _uploadedHeadCenter.Y;
                var radial = Math.Exp(-((dx * dx) / (2 * sigmaX * sigmaX) + (dy * dy) / (2 * sigmaY * sigmaY)));
                var lowerBodyLock = 1.0 - Math.Clamp((y - (bounds.Y + bounds.Height * 0.48)) / Math.Max(1.0, bounds.Height * 0.34), 0, 1);
                lowerBodyLock = lowerBodyLock * lowerBodyLock;
                var weight = Math.Clamp(radial * lowerBodyLock, 0, 1);

                var localPullX = pullX * 1.35;
                var localPullY = pullY * 1.15;
                var rotX = -dy * theta * 0.72 * weight;
                var rotY = dx * theta * 0.45 * weight;
                var sourceX = x - localPullX * weight - rotX;
                var sourceY = y - localPullY * weight - rotY;
                SampleBgra(_uploadedBasePixels, output, size, size, stride, x, y, sourceX, sourceY);
            }
        }

        var warped = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, output, stride);
        warped.Freeze();
        PetImage.Source = warped;
    }

    private static void SampleBgra(byte[] source, byte[] output, int width, int height, int stride, int targetX, int targetY, double sourceX, double sourceY)
    {
        var outputIndex = targetY * stride + targetX * 4;
        if (sourceX < 0 || sourceY < 0 || sourceX >= width - 1 || sourceY >= height - 1)
        {
            output[outputIndex + 3] = 0;
            return;
        }

        var x0 = (int)Math.Floor(sourceX);
        var y0 = (int)Math.Floor(sourceY);
        var fx = sourceX - x0;
        var fy = sourceY - y0;

        var i00 = y0 * stride + x0 * 4;
        var i10 = y0 * stride + (x0 + 1) * 4;
        var i01 = (y0 + 1) * stride + x0 * 4;
        var i11 = (y0 + 1) * stride + (x0 + 1) * 4;

        for (var c = 0; c < 4; c++)
        {
            var top = source[i00 + c] * (1 - fx) + source[i10 + c] * fx;
            var bottom = source[i01 + c] * (1 - fx) + source[i11 + c] * fx;
            output[outputIndex + c] = (byte)Math.Clamp((int)Math.Round(top * (1 - fy) + bottom * fy), 0, 255);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownScreenPoint = PointToScreen(e.GetPosition(this));
        _dragCompleted = false;

        try
        {
            _isDragMoveActive = true;
            DragMove();
            _dragCompleted = true;
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw when the mouse is released before dragging starts.
        }
        finally
        {
            _isDragMoveActive = false;
            SavePosition();
        }
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var mouseUpPoint = PointToScreen(e.GetPosition(this));
        var moved = Math.Abs(mouseUpPoint.X - _mouseDownScreenPoint.X) > 4 ||
                    Math.Abs(mouseUpPoint.Y - _mouseDownScreenPoint.Y) > 4;

        SavePosition();

        if (!_dragCompleted && !moved)
        {
            PlayClickBounce();
        }
    }

    private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ChangeScale(e.Delta > 0 ? 0.1 : -0.1);
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ChangeScale(0.1);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ChangeScale(-0.1);

    private void ChangeImage_Click(object sender, RoutedEventArgs e) => ChangeImage();

    public async void ChangeImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择宠物图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp|透明 PNG|*.png|所有文件|*.*",
            CheckFileExists = true
        };

        if (!string.IsNullOrWhiteSpace(_settings.LastOpenDirectory) &&
            Directory.Exists(_settings.LastOpenDirectory))
        {
            dialog.InitialDirectory = _settings.LastOpenDirectory;
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _settings.LastOpenDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
        IsEnabled = false;
        Cursor = System.Windows.Input.Cursors.Wait;
        PetImage.ToolTip = "正在生成宠物...";
        DefaultPet.ToolTip = "正在生成宠物...";

        try
        {
            var result = await _petImageService.ImportImageAsync(dialog.FileName);
            if (!result.Success || string.IsNullOrWhiteSpace(result.ProcessedImagePath))
            {
                SaveSettings();
                System.Windows.MessageBox.Show(this, result.ErrorMessage ?? "图片处理失败。", "PawDesk", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _settings.CurrentPetImagePath = result.ProcessedImagePath;
            LoadPetImage();
            SaveSettings();
        }
        finally
        {
            IsEnabled = true;
            Cursor = System.Windows.Input.Cursors.Hand;
            PetImage.ToolTip = null;
            DefaultPet.ToolTip = null;
        }
    }

    public void SetScale(double scale)
    {
        _settings.PetScale = Math.Clamp(Math.Round(scale, 2), MinScale, MaxScale);
        ApplyScale();
        SaveSettings();
    }

    public void SetAnimationEnabled(bool enabled)
    {
        _settings.AnimationEnabled = enabled;
        AnimationMenuItem.IsChecked = enabled;
        if (enabled)
        {
            StartAnimations();
        }
        else
        {
            StopAnimations();
        }

        SaveSettings();
    }

    public void SetAlwaysOnTop(bool enabled)
    {
        _settings.AlwaysOnTop = enabled;
        TopmostMenuItem.IsChecked = enabled;
        Topmost = enabled;
        SaveSettings();
    }

    private void ResetScale_Click(object sender, RoutedEventArgs e)
    {
        _settings.PetScale = 1.0;
        ApplyScale();
        SaveSettings();
    }

    private void Animation_Click(object sender, RoutedEventArgs e)
    {
        SetAnimationEnabled(AnimationMenuItem.IsChecked);
    }

    private void Topmost_Click(object sender, RoutedEventArgs e)
    {
        SetAlwaysOnTop(TopmostMenuItem.IsChecked);
    }

    private void Startup_Click(object sender, RoutedEventArgs e)
    {
        _settings.StartWithWindows = StartupMenuItem.IsChecked;
        _startupService.SetEnabled(_settings.StartWithWindows);
        SaveSettings();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => _openSettings();

    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        SavePosition();
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SavePosition();
    }

    private void ChangeScale(double delta)
    {
        SetScale(_settings.PetScale + delta);
    }

    private void SavePosition()
    {
        _settings.PetX = Left;
        _settings.PetY = Top;
        SaveSettings();
    }

    private void SaveSettings()
    {
        _settingsService.Save(_settings);
    }
}
