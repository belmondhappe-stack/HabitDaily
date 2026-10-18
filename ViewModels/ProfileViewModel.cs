using System.Collections.ObjectModel;
using HabitDaily.Services;
using SQLite;

namespace HabitDaily.ViewModels;

public sealed class ProfileViewModel : BaseViewModel
{
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _initials = string.Empty;
    private string _levelBadge = string.Empty;
    private int _currentStreak;
    private int _bestStreak;
    private int _habitCount;
    private double _totalCo2Saved;
    private bool _isBusy;

    public ObservableCollection<ProfileWeekDay> WeekDays { get; } = new();

    public string FullName
    {
        get => _fullName;
        private set => SetProperty(ref _fullName, value);
    }

    public string Email
    {
        get => _email;
        private set => SetProperty(ref _email, value);
    }

    public string Initials
    {
        get => _initials;
        private set => SetProperty(ref _initials, value);
    }

    public string LevelBadge
    {
        get => _levelBadge;
        private set => SetProperty(ref _levelBadge, value);
    }

    public int CurrentStreak
    {
        get => _currentStreak;
        private set => SetProperty(ref _currentStreak, value);
    }

    public int BestStreak
    {
        get => _bestStreak;
        private set => SetProperty(ref _bestStreak, value);
    }

    public int HabitCount
    {
        get => _habitCount;
        private set => SetProperty(ref _habitCount, value);
    }

    public double TotalCo2Saved
    {
        get => _totalCo2Saved;
        private set => SetProperty(ref _totalCo2Saved, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var user = await DatabaseService.GetCurrentUserAsync();
            if (user == null)
            {
                await CurrentPage.DisplayAlertAsync(
                    "Session introuvable",
                    "Connectez-vous pour consulter votre profil.",
                    "OK");
                return;
            }

            var dashboard = await DatabaseService.GetDashboardDataAsync(user.Id);
            FullName = dashboard.User.FullName;
            Email = dashboard.User.Email;
            Initials = GetInitials(FullName);
            LevelBadge = dashboard.User.LevelBadge;
            CurrentStreak = dashboard.User.CurrentStreak;
            BestStreak = dashboard.User.BestStreak;
            HabitCount = dashboard.Habits.Count;
            TotalCo2Saved = dashboard.User.TotalCo2Saved;
            BuildWeekDays(dashboard.Logs.Select(log => log.LogDate.Date).ToHashSet());
        }
        catch (SQLiteException)
        {
            await CurrentPage.DisplayAlertAsync(
                "Données indisponibles",
                "Impossible de charger votre profil.",
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

    private void BuildWeekDays(HashSet<DateTime> completedDates)
    {
        var today = DateTime.Today;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        WeekDays.Clear();
        for (var offset = 0; offset < 7; offset++)
        {
            var date = monday.AddDays(offset);
            WeekDays.Add(new ProfileWeekDay(
                date.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))[..1].ToUpperInvariant(),
                date.Day.ToString(),
                completedDates.Contains(date),
                date == today));
        }
    }

    private static string GetInitials(string fullName)
    {
        var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));
    }
}

public sealed record ProfileWeekDay(string DayLabel, string DayNumber, bool HasCompletions, bool IsToday)
{
    public Color BackgroundColor => IsToday
        ? Color.FromArgb("#2BD38A")
        : HasCompletions
            ? Color.FromArgb("#236447")
            : Color.FromArgb("#353936");

    public Color TextColor => IsToday
        ? Color.FromArgb("#101A14")
        : HasCompletions
            ? Color.FromArgb("#FFFFFF")
            : Color.FromArgb("#A8B0AB");
}
