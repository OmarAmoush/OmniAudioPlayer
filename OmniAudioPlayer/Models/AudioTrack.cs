using CommunityToolkit.Mvvm.ComponentModel;

namespace OmniAudioPlayer.Models;

public partial class AudioTrack : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = "فنان غير معروف";
    public string Album { get; set; } = "ألبوم غير معروف";
    public string FilePath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isFavorite;

    public string FormattedSize =>
        FileSizeBytes >= 1024 * 1024
            ? $"{(FileSizeBytes / (1024.0 * 1024.0)):F2} MB"
            : $"{(FileSizeBytes / 1024.0):F0} KB";

    public string FormattedDuration =>
        Duration.TotalHours >= 1
            ? Duration.ToString(@"hh\:mm\:ss")
            : Duration.ToString(@"mm\:ss");

    public string FormatBadge => Extension.TrimStart('.').ToUpperInvariant();
}
