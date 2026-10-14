using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using OmniAudioPlayer.Services;
using OmniAudioPlayer.ViewModels;
using OmniAudioPlayer.Views;

namespace OmniAudioPlayer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register Audio Services & ViewModels
        builder.Services.AddSingleton<IAudioPlayerService, NativeAudioPlayerService>();
        builder.Services.AddSingleton<AudioScannerService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
