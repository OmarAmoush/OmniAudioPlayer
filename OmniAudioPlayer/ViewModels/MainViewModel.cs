using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniAudioPlayer.Helpers;
using OmniAudioPlayer.Models;
using OmniAudioPlayer.Services;

namespace OmniAudioPlayer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAudioPlayerService _playerService;
    private readonly AudioScannerService _scannerService;
    private readonly Random _random = new();
    private IDispatcherTimer? _sleepTimer;

    public ObservableCollection<AudioTrack> AllTracks { get; } = new();
    public ObservableCollection<AudioTrack> FilteredTracks { get; } = new();

    [ObservableProperty]
    private AudioTrack? _currentTrack;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _currentPositionSeconds;

    [ObservableProperty]
    private double _totalDurationSeconds = 1;

    [ObservableProperty]
    private string _currentPositionText = "00:00";

    [ObservableProperty]
    private string _totalDurationText = "00:00";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedExtensionFilter = "الكل";

    [ObservableProperty]
    private double _playbackSpeed = 1.0;

    [ObservableProperty]
    private double _volume = 1.0;

    [ObservableProperty]
    private bool _isShuffle;

    [ObservableProperty]
    private string _repeatModeIcon = "🔁 إيقاف التكرار";

    [ObservableProperty]
    private string _sleepTimerStatus = "مؤقت النوم: متوقف";

    [ObservableProperty]
    private bool _showFavoritesOnly;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "جاهز لتشغيل جميع الصيغ الصوتية";

    public ObservableCollection<string> AvailableExtensions { get; } = new()
    {
        "الكل", "MP3", "WAV", "FLAC", "M4A", "AAC", "OGG", "OPUS", "WMA", "AMR", "AIFF", "ALAC", "MID"
    };

    public MainViewModel(IAudioPlayerService playerService, AudioScannerService scannerService)
    {
        _playerService = playerService;
        _scannerService = scannerService;

        _playerService.TrackChanged += (_, track) => CurrentTrack = track;
        _playerService.PlaybackStateChanged += (_, playing) =>
        {
            IsPlaying = playing;
            if (!playing && CurrentPositionSeconds >= TotalDurationSeconds - 0.5 && AllTracks.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(async () => await NextTrackAsync());
            }
        };
        _playerService.PositionChanged += (_, pos) =>
        {
            CurrentPositionSeconds = pos.TotalSeconds;
            CurrentPositionText = FormatTime(pos);
        };
        _playerService.DurationChanged += (_, dur) =>
        {
            TotalDurationSeconds = Math.Max(1, dur.TotalSeconds);
            TotalDurationText = FormatTime(dur);
        };
        _playerService.PlaybackError += (_, err) => StatusMessage = err;
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedExtensionFilterChanged(string value) => ApplyFilters();
    partial void OnShowFavoritesOnlyChanged(bool value) => ApplyFilters();

    partial void OnPlaybackSpeedChanged(double value)
    {
        _playerService.PlaybackSpeed = value;
    }

    partial void OnVolumeChanged(double value)
    {
        _playerService.Volume = value;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await RequestStoragePermissionsAsync();
        await ScanDeviceAsync();
    }

    [RelayCommand]
    public async Task ScanDeviceAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = "جاري فحص الجهاز بحثاً عن كافة الملفات الصوتية...";

        try
        {
            var found = await _scannerService.ScanDeviceAudioFilesAsync();
            AllTracks.Clear();
            foreach (var track in found)
                AllTracks.Add(track);

            ApplyFilters();
            StatusMessage = $"تم العثور على {AllTracks.Count} ملف صوتي بجميع الامتدادات";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ أثناء الفحص: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task PickFilesAsync()
    {
        try
        {
            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, AudioFormats.AndroidMimeTypes },
                { DevicePlatform.WinUI, AudioFormats.SupportedExtensions },
                { DevicePlatform.iOS, new[] { "public.audio" } },
                { DevicePlatform.MacCatalyst, new[] { "public.audio" } }
            });

            var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "اختر ملفاً صوتياً أو أكثر (جميع الامتدادات مدعومة)",
                FileTypes = customFileType
            });

            if (results == null) return;

            AudioTrack? firstAdded = null;
            foreach (var file in results)
            {
                var track = new AudioTrack
                {
                    Title = Path.GetFileNameWithoutExtension(file.FileName),
                    Artist = "ملف مختار",
                    Album = "استيراد مباشر",
                    FilePath = file.FullPath,
                    Extension = Path.GetExtension(file.FileName)
                };
                AllTracks.Insert(0, track);
                firstAdded ??= track;
            }

            ApplyFilters();
            if (firstAdded != null)
            {
                await PlaySelectedTrackAsync(firstAdded);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر فتح مستعرض الملفات: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task PlaySelectedTrackAsync(AudioTrack? track)
    {
        if (track == null) return;
        foreach (var t in AllTracks) t.IsPlaying = false;
        await _playerService.PlayTrackAsync(track);
        StatusMessage = $"يعمل الآن: {track.Title} ({track.FormatBadge})";
    }

    [RelayCommand]
    public async Task TogglePlayPauseAsync()
    {
        if (CurrentTrack == null)
        {
            if (FilteredTracks.Count > 0)
                await PlaySelectedTrackAsync(FilteredTracks[0]);
            return;
        }

        if (IsPlaying)
            await _playerService.PauseAsync();
        else
            await _playerService.ResumeAsync();
    }

    [RelayCommand]
    public async Task NextTrackAsync()
    {
        if (FilteredTracks.Count == 0) return;

        if (IsShuffle)
        {
            var randomTrack = FilteredTracks[_random.Next(FilteredTracks.Count)];
            await PlaySelectedTrackAsync(randomTrack);
            return;
        }

        int idx = CurrentTrack != null ? FilteredTracks.IndexOf(CurrentTrack) : -1;
        if (idx + 1 < FilteredTracks.Count)
        {
            await PlaySelectedTrackAsync(FilteredTracks[idx + 1]);
        }
        else if (_playerService.RepeatMode == RepeatMode.RepeatAll)
        {
            await PlaySelectedTrackAsync(FilteredTracks[0]);
        }
    }

    [RelayCommand]
    public async Task PreviousTrackAsync()
    {
        if (FilteredTracks.Count == 0) return;

        if (CurrentPositionSeconds > 3.0)
        {
            await _playerService.SeekToAsync(TimeSpan.Zero);
            return;
        }

        int idx = CurrentTrack != null ? FilteredTracks.IndexOf(CurrentTrack) : 0;
        int prevIdx = idx > 0 ? idx - 1 : FilteredTracks.Count - 1;
        await PlaySelectedTrackAsync(FilteredTracks[prevIdx]);
    }

    [RelayCommand]
    public async Task SeekAsync(double seconds)
    {
        await _playerService.SeekToAsync(TimeSpan.FromSeconds(seconds));
    }

    [RelayCommand]
    public async Task SkipForward10Async()
    {
        double target = Math.Min(TotalDurationSeconds, CurrentPositionSeconds + 10);
        await _playerService.SeekToAsync(TimeSpan.FromSeconds(target));
    }

    [RelayCommand]
    public async Task Rewind10Async()
    {
        double target = Math.Max(0, CurrentPositionSeconds - 10);
        await _playerService.SeekToAsync(TimeSpan.FromSeconds(target));
    }

    [RelayCommand]
    public void ToggleShuffle()
    {
        IsShuffle = !IsShuffle;
        _playerService.IsShuffleEnabled = IsShuffle;
    }

    [RelayCommand]
    public void CycleRepeatMode()
    {
        _playerService.RepeatMode = _playerService.RepeatMode switch
        {
            RepeatMode.Off => RepeatMode.RepeatAll,
            RepeatMode.RepeatAll => RepeatMode.RepeatOne,
            _ => RepeatMode.Off
        };

        RepeatModeIcon = _playerService.RepeatMode switch
        {
            RepeatMode.RepeatAll => "🔁 تكرار الكل",
            RepeatMode.RepeatOne => "🔂 تكرار المقطع",
            _ => "🔁 إيقاف التكرار"
        };
    }

    [RelayCommand]
    public void ToggleFavorite(AudioTrack? track)
    {
        if (track == null) return;
        track.IsFavorite = !track.IsFavorite;
        if (ShowFavoritesOnly) ApplyFilters();
    }

    [RelayCommand]
    public void ToggleShowFavorites()
    {
        ShowFavoritesOnly = !ShowFavoritesOnly;
    }

    [RelayCommand]
    public void CycleSpeed()
    {
        PlaybackSpeed = PlaybackSpeed switch
        {
            1.0 => 1.25,
            1.25 => 1.5,
            1.5 => 2.0,
            2.0 => 0.75,
            _ => 1.0
        };
    }

    [RelayCommand]
    public void SetSleepTimer(string minutesStr)
    {
        _sleepTimer?.Stop();
        if (!int.TryParse(minutesStr, out int minutes) || minutes <= 0)
        {
            SleepTimerStatus = "مؤقت النوم: متوقف";
            return;
        }

        _sleepTimer = Application.Current!.Dispatcher.CreateTimer();
        _sleepTimer.Interval = TimeSpan.FromMinutes(minutes);
        _sleepTimer.IsRepeating = false;
        _sleepTimer.Tick += async (_, _) =>
        {
            await _playerService.PauseAsync();
            SleepTimerStatus = "مؤقت النوم: تم إيقاف التشغيل تلقائياً";
        };
        _sleepTimer.Start();
        SleepTimerStatus = $"مؤقت النوم: بعد {minutes} دقيقة";
    }

    private void ApplyFilters()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filtered = AllTracks.Where(t =>
        {
            bool matchesSearch = string.IsNullOrEmpty(query) ||
                                 t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 t.Artist.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                 t.FormatBadge.Contains(query, StringComparison.OrdinalIgnoreCase);

            bool matchesExt = SelectedExtensionFilter == "الكل" ||
                              t.FormatBadge.Equals(SelectedExtensionFilter, StringComparison.OrdinalIgnoreCase);

            bool matchesFav = !ShowFavoritesOnly || t.IsFavorite;

            return matchesSearch && matchesExt && matchesFav;
        }).ToList();

        FilteredTracks.Clear();
        foreach (var item in filtered)
            FilteredTracks.Add(item);
    }

    private static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"hh\:mm\:ss") : time.ToString(@"mm\:ss");

    private static async Task RequestStoragePermissionsAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            if (status != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.StorageRead>();
            }
        }
        catch { }
    }
}
