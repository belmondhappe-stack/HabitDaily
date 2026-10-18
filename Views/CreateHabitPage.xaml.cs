using HabitDaily.ViewModels;

namespace HabitDaily.Views;

public partial class CreateHabitPage : ContentPage
{
    public CreateHabitPage()
    {
        InitializeComponent();
        BindingContext = new CreateHabitViewModel();
    }

    private async void OnNotificationsClicked(object? sender, EventArgs e)
    {
        await DisplayAlertAsync(
            "Notifications",
            "Les rappels et notifications seront bientôt disponibles.",
            "OK");
    }
}
