using System.Collections.ObjectModel;
using System.Windows.Input;
using HabitDaily.Models;
using HabitDaily.Services;
using HabitDaily.Views;
using SQLite;

namespace HabitDaily.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private User? _currentUser;
    private bool _isBusy;
    private bool _isUpdatingHabit;

    public User? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ObservableCollection<Habit> TodayHabits { get; } = new();

    public ICommand ToggleHabitCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand AddHabitCommand { get; }

    public DashboardViewModel()
    {
        ToggleHabitCommand = new Command<Habit>(async habit => await ToggleHabitAsync(habit));
        RefreshCommand = new Command(async () => await LoadDataAsync());
        AddHabitCommand = new Command(async () => await OnAddHabitAsync());
    }

    public async Task LoadDataAsync()
    {
        if (IsBusy || _isUpdatingHabit)
            return;

        IsBusy = true;
        try
        {
            var user = await DatabaseService.GetCurrentUserAsync();
            if (user == null)
            {
                CurrentUser = null;
                TodayHabits.Clear();
                await CurrentPage.DisplayAlertAsync(
                    "Session introuvable",
                    "Connectez-vous pour consulter vos habitudes.",
                    "OK");
                return;
            }

            var dashboardData = await DatabaseService.GetDashboardDataAsync(user.Id);
            var today = DateTime.Today;
            var completedToday = dashboardData.Logs
                .Where(log => log.LogDate.Date == today)
                .Select(log => log.HabitId)
                .ToHashSet();

            CurrentUser = dashboardData.User;
            TodayHabits.Clear();
            foreach (var habit in dashboardData.Habits)
            {
                habit.IsCompletedToday = completedToday.Contains(habit.Id);
                TodayHabits.Add(habit);
            }
        }
        catch (SQLiteException)
        {
            await CurrentPage.DisplayAlertAsync(
                "Données indisponibles",
                "Impossible de charger vos habitudes enregistrées.",
                "OK");
        }
        catch (InvalidOperationException exception)
        {
            await CurrentPage.DisplayAlertAsync("Erreur", exception.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ToggleHabitAsync(Habit? habit)
    {
        if (habit == null || IsBusy || _isUpdatingHabit || CurrentUser == null)
            return;

        _isUpdatingHabit = true;
        try
        {
            await DatabaseService.SaveHabitCompletionAsync(
                CurrentUser.Id,
                habit.Id,
                !habit.IsCompletedToday,
                DateTime.Today);
            _isUpdatingHabit = false;
            await LoadDataAsync();
        }
        catch (SQLiteException)
        {
            await CurrentPage.DisplayAlertAsync(
                "Mise à jour impossible",
                "La validation de cette habitude n'a pas pu être enregistrée.",
                "OK");
        }
        catch (InvalidOperationException exception)
        {
            await CurrentPage.DisplayAlertAsync("Erreur", exception.Message, "OK");
        }
        finally
        {
            _isUpdatingHabit = false;
        }
    }

    private async Task OnAddHabitAsync()
    {
        await Shell.Current.GoToAsync(nameof(CreateHabitPage));
    }
}
