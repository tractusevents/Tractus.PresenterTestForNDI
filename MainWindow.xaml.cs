using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Tractus.PresenterTest;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _uiTimer;
    private PresenterEngine? _engine;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => EnableDarkTitleBar();
        _settings = SettingsStore.Load();
        Cards = new ObservableCollection<PresenterCardViewModel>(Enumerable.Range(1, 8)
            .Select(number => new PresenterCardViewModel(
                number,
                _settings.SourceAudioModes[number - 1],
                _settings.CustomImagePaths[number - 1],
                SetIndividualMode)));
        DataContext = this;

        SourceCountComboBox.ItemsSource = Enumerable.Range(1, 8);
        SourceCountComboBox.SelectedItem = _settings.SourceCount;
        NamePrefixTextBox.Text = _settings.NamePrefix;
        ToneLevelTextBox.Text = _settings.ToneLevelDb.ToString("0.#", CultureInfo.InvariantCulture);
        ChaseSecondsTextBox.Text = _settings.ChaseSeconds.ToString("0.#", CultureInfo.InvariantCulture);
        UpdateCardVisibility();
        UpdateProgramText();

        SourceCountComboBox.SelectionChanged += (_, _) => UpdateCardVisibility();
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _uiTimer.Tick += (_, _) => RefreshStatus();
        _uiTimer.Start();
        Closed += (_, _) => Shutdown();
    }

    public ObservableCollection<PresenterCardViewModel> Cards { get; }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ReadSettingsFromControls();
            _engine?.Dispose();
            _engine = new PresenterEngine(_settings);
            _engine.Start();
            _engine.SetProgramMode(_settings.AudioProgram);
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            NamePrefixTextBox.IsEnabled = false;
            SourceCountComboBox.IsEnabled = false;
            ToneLevelTextBox.IsEnabled = false;
            SetImageControlsEnabled(false);
            StatusText.Text = "Starting NDI sources…";
            SettingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            _engine?.Dispose();
            _engine = null;
            MessageBox.Show(this,
                $"The sources could not be started.\n\n{ex.GetBaseException().Message}\n\nInstall the NDI 6.3 runtime or place its x64 DLL beside the application.",
                "NDI runtime error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) => StopEngine();
    private void IndividualButton_Click(object sender, RoutedEventArgs e) => SetProgramMode(AudioProgramMode.Individual);
    private void ToneAllButton_Click(object sender, RoutedEventArgs e) => SetProgramMode(AudioProgramMode.AllTones);
    private void SilenceAllButton_Click(object sender, RoutedEventArgs e) => SetProgramMode(AudioProgramMode.SilenceAll);
    private void ToneChaseButton_Click(object sender, RoutedEventArgs e) => SetProgramMode(AudioProgramMode.ToneChase);

    private void SetProgramMode(AudioProgramMode mode)
    {
        ReadRuntimeControls();
        _settings.AudioProgram = mode;
        _engine?.SetProgramMode(mode);
        UpdateProgramText();
        SettingsStore.Save(_settings);
    }

    private void SetIndividualMode(int number, AudioMode mode)
    {
        _settings.SourceAudioModes[number - 1] = mode;
        _engine?.SetIndividualMode(number, mode);
        SettingsStore.Save(_settings);
    }

    private void ChangeImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int number }) return;
        var dialog = new OpenFileDialog
        {
            Title = $"Choose an image for Presenter {number}",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var importedPath = PresenterImageLoader.ImportCustomImage(number, dialog.FileName);
            _settings.CustomImagePaths[number - 1] = importedPath;
            Cards[number - 1].SetImage(importedPath);
            SettingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"That image could not be loaded.\n\n{ex.GetBaseException().Message}",
                "Image import error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int number }) return;
        _settings.CustomImagePaths[number - 1] = null;
        Cards[number - 1].SetImage(null);
        SettingsStore.Save(_settings);
    }

    private void RefreshStatus()
    {
        var senders = _engine?.Senders ?? [];
        var chase = _engine?.ChasePresenterNumber ?? 0;
        for (var index = 0; index < Cards.Count; index++)
        {
            var card = Cards[index];
            var sender = senders.FirstOrDefault(candidate => candidate.Number == card.Number);
            card.SourceName = sender?.SourceName ?? $"{NamePrefixTextBox.Text.Trim()} {card.Number}";
            card.IsChaseActive = chase == card.Number;
            card.HasFailure = sender?.Failure is not null;
            card.StatusLine = sender is null
                ? "Stopped"
                : sender.Failure is not null
                    ? $"Error: {sender.Failure.GetBaseException().Message}"
                    : $"{sender.MeasuredFps:0.0} fps  •  {sender.Connections} receiver{(sender.Connections == 1 ? "" : "s")}  •  {sender.AudioMode}";
        }

        if (senders.Count > 0)
        {
            var failed = senders.Count(item => item.Failure is not null);
            var started = senders.Count(item => item.FramesSent > 0);
            StatusText.Text = failed > 0
                ? $"{failed} source{(failed == 1 ? "" : "s")} failed — review the highlighted cards"
                : started == senders.Count
                    ? $"All {senders.Count} sources live at 1920×1080p30"
                    : $"Starting sources — {started}/{senders.Count} live";
        }
    }

    private void ReadSettingsFromControls()
    {
        _settings.SourceCount = SourceCountComboBox.SelectedItem is int count ? count : 8;
        _settings.NamePrefix = NamePrefixTextBox.Text;
        ReadRuntimeControls();
        _settings.Normalize();
        UpdateCardVisibility();
    }

    private void ReadRuntimeControls()
    {
        if (double.TryParse(ToneLevelTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var level))
            _settings.ToneLevelDb = level;
        if (double.TryParse(ChaseSecondsTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            _settings.ChaseSeconds = seconds;
        _settings.Normalize();
        ToneLevelTextBox.Text = _settings.ToneLevelDb.ToString("0.#", CultureInfo.InvariantCulture);
        ChaseSecondsTextBox.Text = _settings.ChaseSeconds.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private void UpdateCardVisibility()
    {
        var count = SourceCountComboBox.SelectedItem is int selected ? selected : _settings.SourceCount;
        foreach (var card in Cards)
        {
            card.IsVisible = card.Number <= count;
            card.SourceName = $"{NamePrefixTextBox.Text.Trim()} {card.Number}";
        }
    }

    private void UpdateProgramText()
    {
        ProgramText.Text = _settings.AudioProgram switch
        {
            AudioProgramMode.AllTones => "ALL TONES",
            AudioProgramMode.SilenceAll => "SILENCE ALL",
            AudioProgramMode.ToneChase => $"TONE CHASE • {_settings.ChaseSeconds:0.#}s",
            _ => "INDIVIDUAL"
        };
    }

    private void StopEngine()
    {
        _engine?.Dispose();
        _engine = null;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        NamePrefixTextBox.IsEnabled = true;
        SourceCountComboBox.IsEnabled = true;
        ToneLevelTextBox.IsEnabled = true;
        SetImageControlsEnabled(true);
        StatusText.Text = "Ready — sources are stopped";
        RefreshStatus();
    }

    private void SetImageControlsEnabled(bool enabled)
    {
        foreach (var card in Cards) card.CanChangeImage = enabled;
    }

    private void EnableDarkTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var enabled = 1;
        if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int valueSize);

    private void Shutdown()
    {
        _uiTimer.Stop();
        StopEngine();
        ReadSettingsFromControls();
        SettingsStore.Save(_settings);
    }
}
