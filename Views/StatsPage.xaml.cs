using HabitDaily.ViewModels;

namespace HabitDaily.Views;

public partial class StatsPage : ContentPage
{
    private readonly StatsViewModel _viewModel;

    public StatsPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new StatsViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnNotificationsClicked(object? sender, EventArgs e)
    {
        await DisplayAlertAsync(
            "Notifications",
            "Les rappels et notifications seront bientôt disponibles.",
            "OK");
    }
}
