using OmniAudioPlayer.ViewModels;

namespace OmniAudioPlayer.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    private async void OnSeekSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (Math.Abs(e.NewValue - _viewModel.CurrentPositionSeconds) > 1.5)
        {
            await _viewModel.SeekCommand.ExecuteAsync(e.NewValue);
        }
    }
}
