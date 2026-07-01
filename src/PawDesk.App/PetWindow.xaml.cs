using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
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
    private DateTime _lastMouseReactionUtc = DateTime.MinValue;

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
        _mouseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
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

        if (_settings.AnimationEnabled && _settings.IdleBreathingEnabled)
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
        ReactionTranslate.X = 0;
        ReactionTranslate.Y = 0;
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
        if (!_settings.AnimationEnabled ||
            !_settings.MouseReactionEnabled ||
            DateTime.UtcNow - _lastMouseReactionUtc < TimeSpan.FromSeconds(1))
        {
            return;
        }

        var mouse = System.Windows.Forms.Control.MousePosition;
        var centerX = Left + Width / 2;
        var centerY = Top + Height / 2;
        var dx = mouse.X - centerX;
        var dy = mouse.Y - centerY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance > 120 || distance < 1)
        {
            return;
        }

        _lastMouseReactionUtc = DateTime.UtcNow;
        var offsetX = dx / distance * 10;
        var offsetY = dy / distance * 10;
        AnimateReaction(offsetX, offsetY);
    }

    private void AnimateReaction(double x, double y)
    {
        var duration = TimeSpan.FromMilliseconds(220);
        var ease = new SineEase { EasingMode = EasingMode.EaseOut };

        var xAnimation = new DoubleAnimation(0, x, duration)
        {
            AutoReverse = true,
            EasingFunction = ease
        };
        var yAnimation = new DoubleAnimation(0, y, duration)
        {
            AutoReverse = true,
            EasingFunction = ease
        };

        ReactionTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, xAnimation);
        ReactionTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, yAnimation);
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

        PetImage.Source = image;
        PetImage.Visibility = Visibility.Visible;
        DefaultPet.Visibility = Visibility.Collapsed;
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

        var result = await _petImageService.ImportImageAsync(dialog.FileName);

        IsEnabled = true;
        Cursor = System.Windows.Input.Cursors.Hand;
        PetImage.ToolTip = null;
        DefaultPet.ToolTip = null;
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
