using HabitDaily.ViewModels;

namespace HabitDaily.Views;

public partial class CreateHabitPage : ContentPage
{
    public CreateHabitPage()
    {
        InitializeComponent();
        BindingContext = new CreateHabitViewModel();
    }
}
