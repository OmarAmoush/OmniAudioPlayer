namespace OmniAudioPlayer.Helpers;

public static class AudioFormats
{
    /// <summary>
    /// القائمة الكاملة لجميع امتدادات الملفات الصوتية المدعومة في التطبيق
    /// </summary>
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // الصيغ الشائعة والمضغوطة
        ".mp3", ".aac", ".m4a", ".ogg", ".oga", ".opus", ".wma",
        // الصيغ عالية الدقة وغير المضغوطة (Lossless & Uncompressed)
        ".wav", ".flac", ".alac", ".aiff", ".aif", ".aifc", ".ape", ".wv", ".dsd", ".dsf", ".dff",
        // صيغ الهواتف والتسجيلات الصوتية
        ".amr", ".awb", ".3gp", ".3ga", ".m4b", ".m4p", ".m4r", ".caf",
        // صيغ الموسيقى والمتتبعات (MIDI & Tracker Formats)
        ".mid", ".midi", ".rmi", ".xmf", ".mxmf", ".ota", ".imy",
        // صيغ البث والحاويات الصوتية الإضافية
        ".webm", ".weba", ".ac3", ".eac3", ".dts", ".spx", ".mp2", ".mp1", ".mpa", ".ra", ".rm"
    };

    public static readonly string[] AndroidMimeTypes = new[]
    {
        "audio/*",
        "application/ogg",
        "application/x-flac",
        "application/vnd.apple.mpegurl",
        "application/x-mpegurl",
        "video/3gpp",
        "video/webm"
    };

    public static bool IsAudioFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext);
    }
}
