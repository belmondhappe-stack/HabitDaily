using HabitDaily.Services;
using HabitDaily.Views;
using System.Windows.Input;

namespace HabitDaily.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private string _email = string.Empty;
    private string _password = string.Empty;
    private bool _isBusy;

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

    public ICommand LoginCommand { get; }
    public ICommand GoToSignupCommand { get; }
    public ICommand GoToForgotPasswordCommand { get; }

    public LoginViewModel()
    {
        LoginCommand = new Command(async () => await OnLoginAsync());
        GoToSignupCommand = new Command(async () => await NavigateToAsync(new SignupPage()));
        GoToForgotPasswordCommand = new Command(async () => await NavigateToAsync(new ForgotPasswordPage()));
    }

    private async Task OnLoginAsync()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            await CurrentPage.DisplayAlertAsync("Erreur", "Veuillez remplir tous les champs.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var user = await DatabaseService.GetUserByEmailAsync(Email);
            if (user is null || !DatabaseService.VerifyPassword(Password, user.PasswordHash, out var needsRehash))
            {
                await CurrentPage.DisplayAlertAsync("Erreur", "Utilisateur introuvable ou mot de passe incorrect.", "OK");
                return;
            }

            if (needsRehash)
            {
                user.PasswordHash = DatabaseService.HashPassword(Password);
                await DatabaseService.SaveUserAsync(user);
            }

            SetRootPage(new AppShell());
        }
        catch (SQLite.SQLiteException)
        {
            await CurrentPage.DisplayAlertAsync(
                "Erreur",
                "Impossible d'accéder aux données du compte. Veuillez réessayer.",
                "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
