using HabitDaily.ViewModels;

namespace HabitDaily.Views;

public partial class SignupPage : ContentPage
{
    public SignupPage()
    {
        InitializeComponent();
        BindingContext = new SignupViewModel();
    }
}
