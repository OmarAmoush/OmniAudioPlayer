using OmniAudioPlayer.Models;

namespace OmniAudioPlayer.Services;

public enum RepeatMode
{
    Off,
    RepeatAll,
    RepeatOne
}

public interface IAudioPlayerService
{
    event EventHandler<AudioTrack?>? TrackChanged;
    event EventHandler<bool>? PlaybackStateChanged;
    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler<TimeSpan>? DurationChanged;
    event EventHandler<string>? PlaybackError;

    AudioTrack? CurrentTrack { get; }
    bool IsPlaying { get; }
    TimeSpan CurrentPosition { get; }
    TimeSpan TotalDuration { get; }
    double Volume { get; set; }
    double PlaybackSpeed { get; set; }
    bool IsMuted { get; set; }
    RepeatMode RepeatMode { get; set; }
    bool IsShuffleEnabled { get; set; }

    Task PlayTrackAsync(AudioTrack track);
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    Task SeekToAsync(TimeSpan position);
}
