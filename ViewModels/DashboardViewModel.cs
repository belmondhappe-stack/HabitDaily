using System.Collections.ObjectModel;
using System.Windows.Input;
using HabitDaily.Models;
using HabitDaily.Services;
using HabitDaily.Views;

namespace HabitDaily.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private User? _currentUser;

    public User? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    public ObservableCollection<Habit> TodayHabits { get; } = new();

    public ICommand ToggleHabitCommand { get; }
    public ICommand AddHabitCommand { get; }

    public DashboardViewModel()
    {
        ToggleHabitCommand = new Command<Habit>(OnToggleHabit);
        AddHabitCommand = new Command(async () => await OnAddHabitAsync());

        LoadMockData();
    }

    public async Task LoadSavedHabitsAsync()
    {
        var user = await DatabaseService.GetUserAsync();
        if (user == null)
            return;

        CurrentUser = user;
        var savedHabits = await DatabaseService.GetHabitsByUserAsync(user.Id);
        foreach (var habit in savedHabits)
        {
            if (habit.Id != 0 && !TodayHabits.Any(existingHabit => existingHabit.Id == habit.Id))
                TodayHabits.Add(habit);
        }
    }

    private void LoadMockData()
    {
        CurrentUser = new User
        {
            FullName = "Happe Wakam",
            Email = "exemple@gmail.com",
            TotalCo2Saved = 0.5,
            CurrentStreak = 12,
            LevelBadge = "Éco-championne - Niv. 1"
        };

        TodayHabits.Clear();
        TodayHabits.Add(new Habit { Title = "Faire ses devoirs", Subtitle = "4 devoirs sur 5", IsCompletedToday = false });
        TodayHabits.Add(new Habit { Title = "Se lever à 4h00", Subtitle = "Quotidienne", IsCompletedToday = true });
        TodayHabits.Add(new Habit { Title = "Arroser les plantes", Subtitle = "Écogeste", IsCompletedToday = false });
        TodayHabits.Add(new Habit { Title = "Éteindre les veilles", Subtitle = "Économie d'énergie", IsCompletedToday = true });
    }

    private void OnToggleHabit(Habit? habit)
    {
        if (habit == null) return;
        habit.IsCompletedToday = !habit.IsCompletedToday;
        
        if (habit.IsCompletedToday && CurrentUser != null)
        {
            CurrentUser.TotalCo2Saved += 0.2;
            CurrentUser.CurrentStreak += 1;
            OnPropertyChanged(nameof(CurrentUser));
        }
    }

    private async Task OnAddHabitAsync()
    {
        await Shell.Current.GoToAsync(nameof(CreateHabitPage));
    }
}