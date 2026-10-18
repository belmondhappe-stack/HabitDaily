using HabitDaily.ViewModels;

namespace HabitDaily.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new ProfileViewModel();
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
