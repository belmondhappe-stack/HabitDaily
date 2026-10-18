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

    private async void OnDateSelected(object? sender, DateChangedEventArgs e)
    {
        await _viewModel.SelectDateAsync(e.NewDate ?? DateTime.Today);
    }
}
