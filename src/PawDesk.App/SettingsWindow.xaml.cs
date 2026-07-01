using System.IO;
using System.Windows;
using PawDesk.App.Models;
using PawDesk.App.Services;

namespace PawDesk.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly AppSettingsService _settingsService;
    private readonly StartupService _startupService;
    private readonly PetWindow _petWindow;
    private bool _isLoading;

    public SettingsWindow(
        AppSettings settings,
        AppSettingsService settingsService,
        StartupService startupService,
        PetWindow petWindow)
    {
        InitializeComponent();

        _settings = settings;
        _settingsService = settingsService;
        _startupService = startupService;
        _petWindow = petWindow;
        LoadSettings();
    }

    public void RefreshView()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        _isLoading = true;
        ImagePathText.Text = string.IsNullOrWhiteSpace(_settings.CurrentPetImagePath)
            ? "正在使用默认宠物。"
            : Path.GetFileName(_settings.CurrentPetImagePath);
        ScaleSlider.Value = _settings.PetScale;
        TopmostCheckBox.IsChecked = _settings.AlwaysOnTop;
        AnimationCheckBox.IsChecked = _settings.AnimationEnabled;
        StartupCheckBox.IsChecked = _startupService.IsEnabled();
        _isLoading = false;
    }

    private void ChangeImage_Click(object sender, RoutedEventArgs e)
    {
        _petWindow.ChangeImage();
        RefreshView();
    }

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        _petWindow.SetScale(e.NewValue);
    }

    private void TopmostCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _petWindow.SetAlwaysOnTop(TopmostCheckBox.IsChecked == true);
    }

    private void AnimationCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _petWindow.SetAnimationEnabled(AnimationCheckBox.IsChecked == true);
    }

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        var enabled = StartupCheckBox.IsChecked == true;
        _settings.StartWithWindows = enabled;
        _startupService.SetEnabled(enabled);
        _settingsService.Save(_settings);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _petWindow.SetScale(1.0);
        _petWindow.SetAlwaysOnTop(true);
        _petWindow.SetAnimationEnabled(true);
        RefreshView();
    }
}
