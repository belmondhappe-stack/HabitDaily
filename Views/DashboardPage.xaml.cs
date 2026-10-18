namespace HabitDaily.Views;

public partial class DashboardPage : ContentPage
{
    private readonly ViewModels.DashboardViewModel? _viewModel;

    public DashboardPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new ViewModels.DashboardViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel == null)
            return;

        await _viewModel.LoadDataAsync();
    }
}