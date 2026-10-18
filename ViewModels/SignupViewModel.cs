using HabitDaily.Models;
using HabitDaily.Services;
using System.Windows.Input;

namespace HabitDaily.ViewModels
{
    public class SignupViewModel : BaseViewModel
    {
        private string _fullName = string.Empty;
        private string _email = string.Empty;
        private string _password = string.Empty;
        private bool _isBusy;

        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public ICommand SignupCommand { get; }
        public ICommand GoBackCommand { get; }

        public SignupViewModel()
        {
            SignupCommand = new Command(async () => await OnSignupAsync());
            GoBackCommand = new Command(async () => await GoBackAsync());
        }

        private async Task OnSignupAsync()
        {
            if (IsBusy)
                return;

            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await CurrentPage.DisplayAlertAsync("Erreur", "Veuillez remplir tous les champs du formulaire.", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                var existingUser = await DatabaseService.GetUserByEmailAsync(Email);
                if (existingUser != null)
                {
                    await CurrentPage.DisplayAlertAsync("Erreur", "Un compte existe déjà avec cet email.", "OK");
                    return;
                }

                var newUser = new User
                {
                    FullName = FullName.Trim(),
                    Email = Email.Trim(),
                    PasswordHash = DatabaseService.HashPassword(Password)
                };

                await DatabaseService.SaveUserAsync(newUser);
                DatabaseService.SetCurrentUser(newUser);

                await CurrentPage.DisplayAlertAsync("Succès", "Compte créé avec succès !", "OK");
                SetRootPage(new AppShell());
            }
            catch (SQLite.SQLiteException)
            {
                await CurrentPage.DisplayAlertAsync("Erreur", "Impossible d'enregistrer le compte. Veuillez réessayer.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}