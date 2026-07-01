using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
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
        UploadedHeadRotate.Angle = 0;
        UploadedHeadTranslate.X = 0;
        UploadedHeadTranslate.Y = 0;
        BounceTranslate.Y = 0;
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
        AnimateTo(HeadTranslate, System.Windows.Media.TranslateTransform.XProperty, headX, duration);
        AnimateTo(HeadTranslate, System.Windows.Media.TranslateTransform.YProperty, headY, duration);
        AnimateTo(HeadRotate, System.Windows.Media.RotateTransform.AngleProperty, headAngle, duration);

        var imageIsVisible = UploadedPet.Visibility == Visibility.Visible;
        if (imageIsVisible)
        {
            AnimateTo(HeadTranslate, System.Windows.Media.TranslateTransform.XProperty, 0, duration);
            AnimateTo(HeadTranslate, System.Windows.Media.TranslateTransform.YProperty, 0, duration);
            AnimateTo(HeadRotate, System.Windows.Media.RotateTransform.AngleProperty, 0, duration);
            AnimateTo(UploadedHeadTranslate, System.Windows.Media.TranslateTransform.XProperty, headX, duration);
            AnimateTo(UploadedHeadTranslate, System.Windows.Media.TranslateTransform.YProperty, headY * 0.7, duration);
            AnimateTo(UploadedHeadRotate, System.Windows.Media.RotateTransform.AngleProperty, headAngle, duration);
            AnimateTo(LeanRotate, System.Windows.Media.RotateTransform.AngleProperty, 0, duration);
            AnimateTo(ReactionTranslate, System.Windows.Media.TranslateTransform.XProperty, 0, duration);
            AnimateTo(ReactionTranslate, System.Windows.Media.TranslateTransform.YProperty, 0, duration);
            return;
        }

        AnimateTo(UploadedHeadTranslate, System.Windows.Media.TranslateTransform.XProperty, 0, duration);
        AnimateTo(UploadedHeadTranslate, System.Windows.Media.TranslateTransform.YProperty, 0, duration);
        AnimateTo(UploadedHeadRotate, System.Windows.Media.RotateTransform.AngleProperty, 0, duration);
        AnimateTo(LeanRotate, System.Windows.Media.RotateTransform.AngleProperty, 0, duration);
        AnimateTo(ReactionTranslate, System.Windows.Media.TranslateTransform.XProperty, 0, duration);
        AnimateTo(ReactionTranslate, System.Windows.Media.TranslateTransform.YProperty, 0, duration);
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
            PetBodyImage.Source = null;
            PetHeadImage.Source = null;
            UploadedPet.Visibility = Visibility.Collapsed;
            DefaultPet.Visibility = Visibility.Visible;
            return;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(_settings.CurrentPetImagePath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();

        PetBodyImage.Source = image;
        PetHeadImage.Source = image;
        ApplyUploadedPetRegions(_settings.CurrentPetImagePath, image.PixelWidth, image.PixelHeight);
        UploadedPet.Visibility = Visibility.Visible;
        DefaultPet.Visibility = Visibility.Collapsed;
    }

    private void ApplyUploadedPetRegions(string imagePath, int imageWidth, int imageHeight)
    {
        var foreground = DetectForegroundBounds(imagePath);
        if (foreground is null)
        {
            PetHeadClip.Rect = new Rect(0, 0, BaseSize, BaseSize * 0.64);
            PetBodyClip.Rect = new Rect(0, BaseSize * 0.42, BaseSize, BaseSize * 0.58);
            PetHeadImage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.35);
            return;
        }

        var scale = Math.Min(BaseSize / imageWidth, BaseSize / imageHeight);
        var displayedWidth = imageWidth * scale;
        var displayedHeight = imageHeight * scale;
        var offsetX = (BaseSize - displayedWidth) / 2;
        var offsetY = (BaseSize - displayedHeight) / 2;

        var x = offsetX + foreground.Value.X * scale;
        var y = offsetY + foreground.Value.Y * scale;
        var width = foreground.Value.Width * scale;
        var height = foreground.Value.Height * scale;

        var headHeight = Math.Clamp(height * 0.52, 48, height);
        var overlap = Math.Clamp(height * 0.16, 12, 34);
        var headRect = ClampToPetCanvas(new Rect(x, y, width, headHeight + overlap));
        var bodyRect = ClampToPetCanvas(new Rect(x, y + Math.Max(0, headHeight - overlap), width, height - headHeight + overlap));

        PetHeadClip.Rect = headRect;
        PetBodyClip.Rect = bodyRect;
        PetHeadImage.RenderTransformOrigin = new System.Windows.Point(
            Math.Clamp((headRect.X + headRect.Width / 2) / BaseSize, 0.1, 0.9),
            Math.Clamp((headRect.Y + headRect.Height * 0.62) / BaseSize, 0.15, 0.85));
    }

    private static Int32Rect? DetectForegroundBounds(string imagePath)
    {
        try
        {
            using var stream = File.OpenRead(imagePath);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            var converted = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            var stride = converted.PixelWidth * 4;
            var pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);

            var minX = converted.PixelWidth;
            var minY = converted.PixelHeight;
            var maxX = -1;
            var maxY = -1;

            for (var y = 0; y < converted.PixelHeight; y++)
            {
                for (var x = 0; x < converted.PixelWidth; x++)
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

            if (maxX < minX || maxY < minY)
            {
                return null;
            }

            return new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        catch
        {
            return null;
        }
    }

    private static Rect ClampToPetCanvas(Rect rect)
    {
        var x = Math.Clamp(rect.X, 0, BaseSize);
        var y = Math.Clamp(rect.Y, 0, BaseSize);
        var right = Math.Clamp(rect.Right, 0, BaseSize);
        var bottom = Math.Clamp(rect.Bottom, 0, BaseSize);
        return new Rect(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
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
        UploadedPet.ToolTip = "正在生成宠物...";
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
            UploadedPet.ToolTip = null;
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
