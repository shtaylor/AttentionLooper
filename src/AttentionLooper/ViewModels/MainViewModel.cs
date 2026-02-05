using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using AttentionLooper.Models;
using AttentionLooper.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AttentionLooper.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly SoundLibrary _soundLibrary;
    private readonly ChimeController _chimeController;
    private readonly ThemeService _themeService;
    private readonly DispatcherTimer _countdownTimer;
    private readonly Dispatcher _dispatcher;

    private string? _currentSoundPath;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string? _selectedSoundName;

    [ObservableProperty]
    private TimeSpan? _periodTimeSpan = TimeSpan.FromMinutes(4);

    [ObservableProperty]
    private string _periodErrorMessage = "";

    [ObservableProperty]
    private string _countdownText = "";

    [ObservableProperty]
    private float[]? _waveformPeaks;

    [ObservableProperty]
    private bool _isLogVisible;

    [ObservableProperty]
    private string _selectedTheme = "Dark";

    public ObservableCollection<string> SoundNames { get; } = [];
    public ObservableCollection<string> LogMessages { get; } = [];
    public string[] ThemeOptions { get; } = ["Dark", "Light", "System"];

    public MainViewModel()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _themeService = new ThemeService();

        var libraryDir = Path.Combine(AppContext.BaseDirectory, "AudioLibrary");
        _soundLibrary = new SoundLibrary(libraryDir);

        _chimeController = new ChimeController(GetSoundPath, AddLog);
        _chimeController.StateChanged += OnChimeStateChanged;
        _chimeController.SoundPlaybackError += OnSoundPlaybackError;

        _countdownTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _countdownTimer.Tick += CountdownTimer_Tick;

        // Load saved theme
        var savedTheme = _themeService.LoadPreference();
        _selectedTheme = savedTheme;
        _themeService.ApplyTheme(savedTheme);

        // Populate sounds
        PopulateSounds();

        // Select default sound
        var names = _soundLibrary.GetSoundNamesSorted();
        if (names.Count > 0)
        {
            var preferred =
                names.FirstOrDefault(n => n.Equals("wood-block", StringComparison.OrdinalIgnoreCase))
                ?? names.FirstOrDefault(n => n.Equals("chime", StringComparison.OrdinalIgnoreCase))
                ?? names[0];
            SelectedSoundName = preferred;
        }

        // Validate initial period
        ValidatePeriod();
    }

    partial void OnSelectedSoundNameChanged(string? value)
    {
        if (value == null) return;

        if (_soundLibrary.TryGetPath(value, out var path))
        {
            Volatile.Write(ref _currentSoundPath, path);
            LoadWaveformAsync(path);
        }
    }

    partial void OnPeriodTimeSpanChanged(TimeSpan? value)
    {
        ValidatePeriod();
    }

    partial void OnSelectedThemeChanged(string value)
    {
        _themeService.ApplyTheme(value);
    }

    [RelayCommand]
    private void TogglePlayStop()
    {
        if (IsPlaying)
        {
            _chimeController.Stop();
            _countdownTimer.Stop();
            IsPlaying = false;
            CountdownText = "";
        }
        else
        {
            if (!ValidatePeriod()) return;
            _chimeController.Play();
            _countdownTimer.Start();
            IsPlaying = true;
        }
    }

    [RelayCommand]
    private void RefreshSounds()
    {
        _soundLibrary.Refresh();
        var currentSelection = SelectedSoundName;
        PopulateSounds();

        // Preserve selection if still valid
        if (currentSelection != null && SoundNames.Contains(currentSelection))
        {
            SelectedSoundName = currentSelection;
        }
        else if (SoundNames.Count > 0)
        {
            SelectedSoundName = SoundNames[0];
        }
    }

    private bool ValidatePeriod()
    {
        if (!PeriodTimeSpan.HasValue || PeriodTimeSpan.Value <= TimeSpan.Zero)
        {
            PeriodErrorMessage = "Period must be > 0.";
            return false;
        }

        PeriodErrorMessage = "";
        try
        {
            _chimeController.SetPeriod(PeriodTimeSpan.Value);
            return true;
        }
        catch (Exception ex)
        {
            PeriodErrorMessage = ex.Message;
            return false;
        }
    }

    private void PopulateSounds()
    {
        SoundNames.Clear();
        foreach (var name in _soundLibrary.GetSoundNamesSorted())
            SoundNames.Add(name);
    }

    private string? GetSoundPath()
    {
        var path = Volatile.Read(ref _currentSoundPath);
        if (path != null && !File.Exists(path))
        {
            // File was deleted - trigger recovery on UI thread
            _dispatcher.BeginInvoke(() => HandleMissingSoundFile(path!));
            return null;
        }
        return path;
    }

    private void HandleMissingSoundFile(string missingPath)
    {
        var fileName = Path.GetFileName(missingPath);
        AddLog($"Sound file missing: {fileName}");

        _soundLibrary.Refresh();
        PopulateSounds();

        var fallback = _soundLibrary.GetFirstAvailableName();
        if (fallback != null)
        {
            AddLog($"Auto-selected: {fallback}");
            SelectedSoundName = fallback;
        }
        else
        {
            AddLog("No sound files available.");
            SelectedSoundName = null;
            Volatile.Write(ref _currentSoundPath, null);
        }

        IsLogVisible = true;
    }

    private void LoadWaveformAsync(string path)
    {
        Task.Run(() =>
        {
            try
            {
                var peaks = AudioService.ReadPeaks(path, 300);
                _dispatcher.BeginInvoke(() => WaveformPeaks = peaks);
            }
            catch
            {
                _dispatcher.BeginInvoke(() => WaveformPeaks = null);
            }
        });
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        var next = _chimeController.NextFireTimeUtc;
        if (next == null)
        {
            CountdownText = "";
            return;
        }

        var remaining = next.Value - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        CountdownText = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2}";
    }

    private void OnChimeStateChanged()
    {
        _dispatcher.BeginInvoke(() =>
        {
            IsPlaying = _chimeController.State == PlayerState.Playing;
        });
    }

    private void OnSoundPlaybackError(string message)
    {
        _dispatcher.BeginInvoke(() =>
        {
            AddLog(message);
            IsLogVisible = true;
        });
    }

    private void AddLog(string message)
    {
        void DoAdd()
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogMessages.Add($"[{timestamp}] {message}");
        }

        if (_dispatcher.CheckAccess())
            DoAdd();
        else
            _dispatcher.BeginInvoke(DoAdd);
    }

    public void Dispose()
    {
        _countdownTimer.Stop();
        _chimeController.StateChanged -= OnChimeStateChanged;
        _chimeController.SoundPlaybackError -= OnSoundPlaybackError;
        _chimeController.Dispose();
    }
}
