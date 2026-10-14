using OmniAudioPlayer.Helpers;
using OmniAudioPlayer.Models;

namespace OmniAudioPlayer.Services;

public class AudioScannerService
{
    public async Task<List<AudioTrack>> ScanDeviceAudioFilesAsync()
    {
        var tracks = new List<AudioTrack>();

#if ANDROID
        tracks.AddRange(await ScanAndroidMediaStoreAsync());
#endif

        // فحص المجلدات الشائعة أيضاً لضمان التقاط الامتدادات النادرة التي قد لا يفهرسها النظام تلقائياً
        var commonFolders = new List<string>();
#if ANDROID
        var extStorage = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;
        if (!string.IsNullOrEmpty(extStorage))
        {
            commonFolders.Add(Path.Combine(extStorage, "Music"));
            commonFolders.Add(Path.Combine(extStorage, "Download"));
            commonFolders.Add(Path.Combine(extStorage, "Podcasts"));
            commonFolders.Add(Path.Combine(extStorage, "Recordings"));
            commonFolders.Add(Path.Combine(extStorage, "Notifications"));
            commonFolders.Add(Path.Combine(extStorage, "Ringtones"));
        }
#endif
        var knownPaths = new HashSet<string>(tracks.Select(t => t.FilePath), StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            foreach (var folder in commonFolders.Where(Directory.Exists))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                    {
                        if (AudioFormats.IsAudioFile(file) && !knownPaths.Contains(file))
                        {
                            var info = new FileInfo(file);
                            tracks.Add(new AudioTrack
                            {
                                Title = Path.GetFileNameWithoutExtension(file),
                                Artist = info.Directory?.Name ?? "ملف محلي",
                                Album = "مجلد الجهاز",
                                FilePath = file,
                                Extension = info.Extension,
                                FileSizeBytes = info.Exists ? info.Length : 0
                            });
                            knownPaths.Add(file);
                        }
                    }
                }
                catch
                {
                    // تجاهل المجلدات المحمية بصلاحيات النظام
                }
            }
        });

        return tracks.OrderBy(t => t.Title).ToList();
    }

#if ANDROID
    private Task<List<AudioTrack>> ScanAndroidMediaStoreAsync()
    {
        return Task.Run(() =>
        {
            var results = new List<AudioTrack>();
            try
            {
                var context = Android.App.Application.Context;
                var uri = Android.Provider.MediaStore.Audio.Media.ExternalContentUri;
                if (uri == null) return results;

                string[] projection =
                {
                    Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Id,
                    Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Title,
                    Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Artist,
                    Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Album,
                    Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Data,
                    Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Size,
                    Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Duration
                };

                using var cursor = context.ContentResolver?.Query(uri, projection, null, null, null);
                if (cursor != null && cursor.MoveToFirst())
                {
                    int titleCol = cursor.GetColumnIndex(Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Title);
                    int artistCol = cursor.GetColumnIndex(Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Artist);
                    int albumCol = cursor.GetColumnIndex(Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Album);
                    int dataCol = cursor.GetColumnIndex(Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Data);
                    int sizeCol = cursor.GetColumnIndex(Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Size);
                    int durCol = cursor.GetColumnIndex(Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Duration);

                    do
                    {
                        string path = dataCol >= 0 ? cursor.GetString(dataCol) ?? "" : "";
                        if (string.IsNullOrEmpty(path)) continue;

                        string title = titleCol >= 0 ? cursor.GetString(titleCol) ?? "" : Path.GetFileNameWithoutExtension(path);
                        string artist = artistCol >= 0 ? cursor.GetString(artistCol) ?? "فنان غير معروف" : "فنان غير معروف";
                        string album = albumCol >= 0 ? cursor.GetString(albumCol) ?? "ألبوم غير معروف" : "ألبوم غير معروف";
                        long size = sizeCol >= 0 ? cursor.GetLong(sizeCol) : 0;
                        long durationMs = durCol >= 0 ? cursor.GetLong(durCol) : 0;

                        if (artist == "<unknown>") artist = "فنان غير معروف";

                        results.Add(new AudioTrack
                        {
                            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(path) : title,
                            Artist = artist,
                            Album = album,
                            FilePath = path,
                            Extension = Path.GetExtension(path),
                            FileSizeBytes = size,
                            Duration = TimeSpan.FromMilliseconds(durationMs)
                        });
                    } while (cursor.MoveToNext());
                }
            }
            catch
            {
                // في حال عدم منح الصلاحية بعد
            }
            return results;
        });
    }
#endif
}
