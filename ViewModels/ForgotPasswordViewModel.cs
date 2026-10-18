using System.Windows.Input;

namespace HabitDaily.ViewModels
{
    public class ForgotPasswordViewModel : BaseViewModel
    {
        public ICommand GoBackCommand { get; }

        public ForgotPasswordViewModel()
        {
            GoBackCommand = new Command(async () => await GoBackAsync());
        }
    }
}