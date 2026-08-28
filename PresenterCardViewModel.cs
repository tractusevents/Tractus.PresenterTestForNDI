using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Tractus.PresenterTest;

public sealed class PresenterCardViewModel : INotifyPropertyChanged
{
    private readonly Action<int, AudioMode> _modeChanged;
    private AudioMode _selectedMode;
    private string _sourceName = string.Empty;
    private string _statusLine = "Stopped";
    private bool _isVisible = true;
    private bool _isChaseActive;
    private bool _hasFailure;
    private string _previewPath;
    private bool _isCustomImage;
    private bool _canChangeImage = true;

    public PresenterCardViewModel(int number, AudioMode mode, string? customImagePath, Action<int, AudioMode> modeChanged)
    {
        Number = number;
        _selectedMode = mode;
        _modeChanged = modeChanged;
        _previewPath = PresenterImageLoader.ResolvePath(number, customImagePath);
        _isCustomImage = !string.IsNullOrWhiteSpace(customImagePath) && File.Exists(customImagePath);
        FrequencyLabel = $"{Frequencies[number - 1]:0} Hz identification tone";
    }

    private static readonly double[] Frequencies = [400, 500, 630, 800, 1000, 1250, 1600, 2000];

    public int Number { get; }
    public string PreviewPath { get => _previewPath; private set => Set(ref _previewPath, value); }
    public bool IsCustomImage { get => _isCustomImage; private set => Set(ref _isCustomImage, value); }
    public bool CanChangeImage { get => _canChangeImage; set => Set(ref _canChangeImage, value); }
    public string FrequencyLabel { get; }
    public AudioMode[] AvailableModes { get; } = Enum.GetValues<AudioMode>();
    public string ChaseBadge => IsChaseActive ? "TONE" : string.Empty;

    public AudioMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (!Set(ref _selectedMode, value)) return;
            _modeChanged(Number, value);
        }
    }

    public void SetImage(string? customImagePath)
    {
        PreviewPath = PresenterImageLoader.ResolvePath(Number, customImagePath);
        IsCustomImage = !string.IsNullOrWhiteSpace(customImagePath) && File.Exists(customImagePath);
    }

    public string SourceName { get => _sourceName; set => Set(ref _sourceName, value); }
    public string StatusLine { get => _statusLine; set => Set(ref _statusLine, value); }
    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }
    public bool HasFailure { get => _hasFailure; set => Set(ref _hasFailure, value); }
    public bool IsChaseActive
    {
        get => _isChaseActive;
        set
        {
            if (Set(ref _isChaseActive, value)) OnPropertyChanged(nameof(ChaseBadge));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
