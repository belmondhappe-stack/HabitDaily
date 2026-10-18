using System.Collections.ObjectModel;
using HabitDaily.Models;
using HabitDaily.Services;
using SQLite;

namespace HabitDaily.ViewModels;

public sealed class StatsViewModel : BaseViewModel
{
    private int _completedActions;
    private int _activeDays;
    private int _monthActiveDays;
    private int _currentStreak;
    private int _bestStreak;
    private int _habitCount;
    private double _totalCo2Saved;
    private double _successRate;
    private bool _isBusy;

    public ObservableCollection<WeeklyActivity> WeeklyActivity { get; } = new();

    public int CompletedActions
    {
        get => _completedActions;
        private set => SetProperty(ref _completedActions, value);
    }

    public int ActiveDays
    {
        get => _activeDays;
        private set => SetProperty(ref _activeDays, value);
    }

    public string RecentActivityLabel => $"{CompletedActions} gestes · {ActiveDays} jours actifs";

    public int MonthActiveDays
    {
        get => _monthActiveDays;
        private set
        {
            if (!SetProperty(ref _monthActiveDays, value))
                return;

            OnPropertyChanged(nameof(MonthProgressLabel));
        }
    }

    public string MonthProgressLabel => $"{MonthActiveDays:00}/{DateTime.Today.Day:00}";

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

    public double SuccessRate
    {
        get => _successRate;
        private set => SetProperty(ref _successRate, value);
    }

    public string SuccessRateLabel => $"{SuccessRate:F0}%";

    public double SuccessProgress => SuccessRate / 100d;

    public bool HasHabits => HabitCount > 0;

    public bool HasNoHabits => !HasHabits;

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
                    "Connectez-vous pour consulter vos statistiques.",
                    "OK");
                return;
            }

            var dashboard = await DatabaseService.GetDashboardDataAsync(user.Id);
            var logs = dashboard.Logs;
            var today = DateTime.Today;
            var thirtyDaysAgo = today.AddDays(-29);
            var recentLogs = logs.Where(log => log.LogDate.Date >= thirtyDaysAgo
                && log.LogDate.Date <= today).ToList();
            var recentCompletedDates = recentLogs
                .Select(log => log.LogDate.Date)
                .ToHashSet();

            CompletedActions = recentLogs.Count;
            ActiveDays = recentCompletedDates.Count;
            MonthActiveDays = logs
                .Where(log => log.LogDate.Date <= today
                    && log.LogDate.Year == today.Year
                    && log.LogDate.Month == today.Month)
                .Select(log => log.LogDate.Date)
                .Distinct()
                .Count();
            CurrentStreak = dashboard.User.CurrentStreak;
            BestStreak = dashboard.User.BestStreak;
            HabitCount = dashboard.Habits.Count;
            TotalCo2Saved = dashboard.User.TotalCo2Saved;
            BuildWeeklyActivity(logs);
            SuccessRate = CalculateSuccessRate(dashboard.Habits, recentLogs, thirtyDaysAgo, today);
            OnPropertyChanged(nameof(RecentActivityLabel));
            OnPropertyChanged(nameof(SuccessRateLabel));
            OnPropertyChanged(nameof(SuccessProgress));
            OnPropertyChanged(nameof(HasHabits));
            OnPropertyChanged(nameof(HasNoHabits));
        }
        catch (SQLiteException)
        {
            await CurrentPage.DisplayAlertAsync(
                "Données indisponibles",
                "Impossible de charger vos statistiques.",
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

    private void BuildWeeklyActivity(IReadOnlyCollection<HabitLog> logs)
    {
        var today = DateTime.Today;
        var currentWeekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var weeklyCounts = Enumerable.Range(0, 8)
            .Select(offset => currentWeekStart.AddDays((offset - 7) * 7))
            .Select(weekStart => (
                WeekStart: weekStart,
                Count: logs.Count(log => log.LogDate.Date >= weekStart
                    && log.LogDate.Date <= today
                    && log.LogDate.Date < weekStart.AddDays(7))))
            .ToList();
        var maxCount = weeklyCounts.Max(item => item.Count);

        WeeklyActivity.Clear();
        foreach (var item in weeklyCounts)
        {
            var barHeight = maxCount == 0 ? 4 : Math.Max(4, 64d * item.Count / maxCount);
            WeeklyActivity.Add(new WeeklyActivity(
                item.WeekStart.ToString("d/M", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")),
                item.Count,
                barHeight));
        }
    }

    private static double CalculateSuccessRate(
        IReadOnlyCollection<Habit> habits,
        IReadOnlyCollection<HabitLog> recentLogs,
        DateTime startDate,
        DateTime endDate)
    {
        var expectedActions = 0;
        var completedExpectedActions = 0;

        foreach (var habit in habits)
        {
            var firstEligibleDate = habit.CreatedAt.Date > startDate
                ? habit.CreatedAt.Date
                : startDate;
            var completedDates = recentLogs
                .Where(log => log.HabitId == habit.Id)
                .Select(log => log.LogDate.Date)
                .ToHashSet();

            for (var date = firstEligibleDate; date <= endDate; date = date.AddDays(1))
            {
                if (!IsScheduledDay(habit, date))
                    continue;

                expectedActions++;
                if (completedDates.Contains(date))
                    completedExpectedActions++;
            }
        }

        return expectedActions == 0
            ? 0
            : Math.Round(completedExpectedActions * 100d / expectedActions, 1);
    }

    private static bool IsScheduledDay(Habit habit, DateTime date) =>
        habit.Frequency switch
        {
            "Hebdomadaire" => date.DayOfWeek == habit.CreatedAt.DayOfWeek,
            "Jours de semaine" => date.DayOfWeek is DayOfWeek.Monday
                or DayOfWeek.Tuesday
                or DayOfWeek.Wednesday
                or DayOfWeek.Thursday
                or DayOfWeek.Friday,
            "Week-ends" => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            _ => true
        };
}

public sealed record WeeklyActivity(string DayLabel, int CompletedCount, double BarHeight);
