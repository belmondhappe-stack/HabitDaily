using HabitDaily.ViewModels;

namespace HabitDaily.Views;

public partial class IconPickerPage : ContentPage
{
    private readonly TaskCompletionSource<HabitIconOption?> _selection =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IconPickerPage(IReadOnlyList<HabitIconOption> icons, HabitIconOption selectedIcon)
    {
        InitializeComponent();

        var viewModel = new IconPickerViewModel(icons, selectedIcon);
        viewModel.IconSelected += OnIconSelected;
        BindingContext = viewModel;
    }

    public Task<HabitIconOption?> Selection => _selection.Task;

    private async void OnIconSelected(object? sender, HabitIconOption icon)
    {
        _selection.TrySetResult(icon);
        await Shell.Current.Navigation.PopModalAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _selection.TrySetResult(null);
    }
}
