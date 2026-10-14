using OmniAudioPlayer.Models;
#if ANDROID
using Android.Media;
using Android.OS;
#endif

namespace OmniAudioPlayer.Services;

public class NativeAudioPlayerService : IAudioPlayerService, IDisposable
{
    public event EventHandler<AudioTrack?>? TrackChanged;
    public event EventHandler<bool>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<TimeSpan>? DurationChanged;
    public event EventHandler<string>? PlaybackError;

    private readonly IDispatcherTimer _positionTimer;
    private double _volume = 1.0;
    private double _playbackSpeed = 1.0;
    private bool _isMuted;

#if ANDROID
    private MediaPlayer? _mediaPlayer;
#endif

    public AudioTrack? CurrentTrack { get; private set; }
    public bool IsPlaying { get; private set; }
    public TimeSpan CurrentPosition { get; private set; } = TimeSpan.Zero;
    public TimeSpan TotalDuration { get; private set; } = TimeSpan.Zero;
    public RepeatMode RepeatMode { get; set; } = RepeatMode.Off;
    public bool IsShuffleEnabled { get; set; }

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 1.0);
            ApplyVolume();
        }
    }

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            _playbackSpeed = Math.Clamp(value, 0.25, 3.0);
            ApplyPlaybackSpeed();
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            ApplyVolume();
        }
    }

    public NativeAudioPlayerService()
    {
        _positionTimer = Application.Current!.Dispatcher.CreateTimer();
        _positionTimer.Interval = TimeSpan.FromMilliseconds(250);
        _positionTimer.Tick += OnPositionTimerTick;
    }

    public async Task PlayTrackAsync(AudioTrack track)
    {
        await StopAsync();

        CurrentTrack = track;
        TrackChanged?.Invoke(this, CurrentTrack);

#if ANDROID
        try
        {
            _mediaPlayer = new MediaPlayer();
            var attributes = new AudioAttributes.Builder()
                ?.SetUsage(AudioUsageKind.Media)
                ?.SetContentType(AudioContentType.Music)
                ?.Build();

            if (attributes != null)
            {
                _mediaPlayer.SetAudioAttributes(attributes);
            }

            _mediaPlayer.SetWakeMode(Android.App.Application.Context, WakeLockFlags.Partial);

            if (track.FilePath.StartsWith("content://", StringComparison.OrdinalIgnoreCase) ||
                track.FilePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                track.FilePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = Android.Net.Uri.Parse(track.FilePath);
                if (uri != null)
                {
                    _mediaPlayer.SetDataSource(Android.App.Application.Context, uri);
                }
            }
            else
            {
                using var fis = new Java.IO.FileInputStream(track.FilePath);
                _mediaPlayer.SetDataSource(fis.FD);
            }

            _mediaPlayer.Completion += OnMediaPlayerCompletion;
            _mediaPlayer.Error += OnMediaPlayerError;

            await Task.Run(() => _mediaPlayer.Prepare());

            TotalDuration = TimeSpan.FromMilliseconds(_mediaPlayer.Duration);
            if (track.Duration == TimeSpan.Zero && TotalDuration > TimeSpan.Zero)
            {
                track.Duration = TotalDuration;
            }
            DurationChanged?.Invoke(this, TotalDuration);

            ApplyVolume();
            ApplyPlaybackSpeed();

            _mediaPlayer.Start();
            IsPlaying = true;
            track.IsPlaying = true;
            _positionTimer.Start();
            PlaybackStateChanged?.Invoke(this, IsPlaying);
        }
        catch (Exception ex)
        {
            PlaybackError?.Invoke(this, $"تعذر تشغيل الملف ({track.Extension}): {ex.Message}");
        }
#else
        await Task.CompletedTask;
#endif
    }

    public Task PauseAsync()
    {
#if ANDROID
        if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
            IsPlaying = false;
            if (CurrentTrack != null) CurrentTrack.IsPlaying = false;
            _positionTimer.Stop();
            PlaybackStateChanged?.Invoke(this, IsPlaying);
        }
#endif
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
#if ANDROID
        if (_mediaPlayer != null && !_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Start();
            ApplyPlaybackSpeed();
            IsPlaying = true;
            if (CurrentTrack != null) CurrentTrack.IsPlaying = true;
            _positionTimer.Start();
            PlaybackStateChanged?.Invoke(this, IsPlaying);
        }
#endif
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _positionTimer.Stop();
        if (CurrentTrack != null) CurrentTrack.IsPlaying = false;
        IsPlaying = false;
        CurrentPosition = TimeSpan.Zero;

#if ANDROID
        if (_mediaPlayer != null)
        {
            try
            {
                _mediaPlayer.Completion -= OnMediaPlayerCompletion;
                _mediaPlayer.Error -= OnMediaPlayerError;
                if (_mediaPlayer.IsPlaying)
                    _mediaPlayer.Stop();
                _mediaPlayer.Reset();
                _mediaPlayer.Release();
            }
            catch { }
            finally
            {
                _mediaPlayer = null;
            }
        }
#endif
        PlaybackStateChanged?.Invoke(this, IsPlaying);
        PositionChanged?.Invoke(this, CurrentPosition);
        return Task.CompletedTask;
    }

    public Task SeekToAsync(TimeSpan position)
    {
#if ANDROID
        if (_mediaPlayer != null)
        {
            int targetMs = (int)Math.Clamp(position.TotalMilliseconds, 0, TotalDuration.TotalMilliseconds);
            _mediaPlayer.SeekTo(targetMs);
            CurrentPosition = TimeSpan.FromMilliseconds(targetMs);
            PositionChanged?.Invoke(this, CurrentPosition);
        }
#endif
        return Task.CompletedTask;
    }

    private void ApplyVolume()
    {
#if ANDROID
        if (_mediaPlayer != null)
        {
            float vol = _isMuted ? 0f : (float)_volume;
            _mediaPlayer.SetVolume(vol, vol);
        }
#endif
    }

    private void ApplyPlaybackSpeed()
    {
#if ANDROID
        if (_mediaPlayer != null && Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            try
            {
                var parms = _mediaPlayer.PlaybackParams ?? new PlaybackParams();
                parms.SetSpeed((float)_playbackSpeed);
                _mediaPlayer.PlaybackParams = parms;
            }
            catch { }
        }
#endif
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
#if ANDROID
        if (_mediaPlayer != null && IsPlaying)
        {
            try
            {
                CurrentPosition = TimeSpan.FromMilliseconds(_mediaPlayer.CurrentPosition);
                PositionChanged?.Invoke(this, CurrentPosition);
            }
            catch { }
        }
#endif
    }

#if ANDROID
    private async void OnMediaPlayerCompletion(object? sender, EventArgs e)
    {
        if (RepeatMode == RepeatMode.RepeatOne && CurrentTrack != null)
        {
            await SeekToAsync(TimeSpan.Zero);
            await ResumeAsync();
        }
        else
        {
            IsPlaying = false;
            if (CurrentTrack != null) CurrentTrack.IsPlaying = false;
            _positionTimer.Stop();
            PlaybackStateChanged?.Invoke(this, false);
        }
    }

    private void OnMediaPlayerError(object? sender, MediaPlayer.ErrorEventArgs e)
    {
        e.Handled = true;
        PlaybackError?.Invoke(this, $"خطأ في فك ترميز الملف الصوتي: {e.What}");
    }
#endif

    public void Dispose()
    {
        _ = StopAsync();
    }
}
