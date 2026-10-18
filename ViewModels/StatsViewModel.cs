using System.Collections.ObjectModel;
using HabitDaily.Models;
using HabitDaily.Services;
using SQLite;

namespace HabitDaily.ViewModels;

public sealed class StatsViewModel : BaseViewModel
{
    private int _completedActions;
    private int _activeDays;
    private int _currentStreak;
    private int _bestStreak;
    private int _habitCount;
    private double _totalCo2Saved;
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
                    "Connectez-vous pour consulter vos statistiques.",
                    "OK");
                return;
            }

            var dashboard = await DatabaseService.GetDashboardDataAsync(user.Id);
            var logs = dashboard.Logs;
            CompletedActions = logs.Count;
            ActiveDays = logs.Select(log => log.LogDate.Date).Distinct().Count();
            CurrentStreak = dashboard.User.CurrentStreak;
            BestStreak = dashboard.User.BestStreak;
            HabitCount = dashboard.Habits.Count;
            TotalCo2Saved = dashboard.User.TotalCo2Saved;
            BuildWeeklyActivity(logs);
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
        var dailyCounts = Enumerable.Range(0, 7)
            .Select(offset => today.AddDays(offset - 6))
            .Select(date => (Date: date, Count: logs.Count(log => log.LogDate.Date == date)))
            .ToList();
        var maxCount = dailyCounts.Max(item => item.Count);

        WeeklyActivity.Clear();
        foreach (var item in dailyCounts)
        {
            var barHeight = maxCount == 0 ? 4 : Math.Max(4, 54d * item.Count / maxCount);
            WeeklyActivity.Add(new WeeklyActivity(
                item.Date.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")),
                item.Count,
                barHeight));
        }
    }
}

public sealed record WeeklyActivity(string DayLabel, int CompletedCount, double BarHeight);
