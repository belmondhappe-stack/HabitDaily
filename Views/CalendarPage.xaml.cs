using HabitDaily.ViewModels;

namespace HabitDaily.Views;

public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _viewModel;

    public CalendarPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new CalendarViewModel();
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
