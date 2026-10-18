using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using HabitDaily.Models;
using HabitDaily.Services;
using SQLite;

namespace HabitDaily.ViewModels;

public sealed class CalendarViewModel : BaseViewModel
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    private DateTime _selectedDate = DateTime.Today;
    private DateTime _displayedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private int _completedCount;
    private double _dailyCo2Saved;
    private bool _isDailyLogsEmpty = true;
    private bool _isBusy;
    private int _loadVersion;
    private List<HabitLogDetails> _monthLogs = new();

    public CalendarViewModel()
    {
        CalendarDays = new ObservableCollection<CalendarDay>();
        DailyLogs = new ObservableCollection<HabitLogDetails>();

        PreviousMonthCommand = new Command(async () => await ChangeMonthAsync(-1));
        NextMonthCommand = new Command(async () => await ChangeMonthAsync(1));
        SelectTodayCommand = new Command(async () => await SelectTodayAsync());
        SelectDayCommand = new Command<CalendarDay>(async day => await SelectDayAsync(day));
        RefreshCommand = new Command(async () => await LoadAsync());

        BuildCalendarDays();
    }

    public ObservableCollection<CalendarDay> CalendarDays { get; }

    public ObservableCollection<HabitLogDetails> DailyLogs { get; }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        private set
        {
            if (!SetProperty(ref _selectedDate, value.Date))
                return;

            OnPropertyChanged(nameof(SelectedDateLabel));
            OnPropertyChanged(nameof(IsTodaySelected));
            UpdateSelectedDay();
        }
    }

    public DateTime DisplayedMonth
    {
        get => _displayedMonth;
        private set
        {
            if (!SetProperty(ref _displayedMonth, new DateTime(value.Year, value.Month, 1)))
                return;

            OnPropertyChanged(nameof(DisplayedMonthLabel));
            OnPropertyChanged(nameof(CalendarGridHeight));
            BuildCalendarDays();
        }
    }

    public string DisplayedMonthLabel =>
        FrenchCulture.TextInfo.ToTitleCase(DisplayedMonth.ToString("MMMM yyyy", FrenchCulture));

    public double CalendarGridHeight
    {
        get
        {
            var firstDay = new DateTime(DisplayedMonth.Year, DisplayedMonth.Month, 1);
            var leadingDays = ((int)firstDay.DayOfWeek + 6) % 7;
            var weekCount = (leadingDays + DateTime.DaysInMonth(DisplayedMonth.Year, DisplayedMonth.Month) + 6) / 7;
            return weekCount * 36;
        }
    }

    public string SelectedDateLabel =>
        FrenchCulture.TextInfo.ToTitleCase(SelectedDate.ToString("dddd d MMMM yyyy", FrenchCulture));

    public bool IsTodaySelected => SelectedDate == DateTime.Today;

    public int CompletedCount
    {
        get => _completedCount;
        private set => SetProperty(ref _completedCount, value);
    }

    public double DailyCo2Saved
    {
        get => _dailyCo2Saved;
        private set => SetProperty(ref _dailyCo2Saved, value);
    }

    public bool IsDailyLogsEmpty
    {
        get => _isDailyLogsEmpty;
        private set => SetProperty(ref _isDailyLogsEmpty, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand PreviousMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand SelectTodayCommand { get; }
    public ICommand SelectDayCommand { get; }
    public ICommand RefreshCommand { get; }

    public async Task LoadAsync()
    {
        var loadVersion = ++_loadVersion;
        IsBusy = true;

        try
        {
            var user = await DatabaseService.GetCurrentUserAsync();
            if (user == null)
            {
                _monthLogs = new List<HabitLogDetails>();
                BuildCalendarDays();
                UpdateSelectedDay();
                await CurrentPage.DisplayAlertAsync(
                    "Session introuvable",
                    "Connectez-vous pour consulter votre calendrier.",
                    "OK");
                return;
            }

            var startDate = new DateTime(DisplayedMonth.Year, DisplayedMonth.Month, 1);
            var endDate = startDate.AddMonths(1);
            var logs = await DatabaseService.GetHabitLogsByDateRangeAsync(user.Id, startDate, endDate);
            if (loadVersion != _loadVersion)
                return;

            _monthLogs = logs;
            BuildCalendarDays();
            UpdateSelectedDay();
        }
        catch (SQLiteException)
        {
            await CurrentPage.DisplayAlertAsync(
                "Données indisponibles",
                "Impossible de charger l’historique de vos écogestes.",
                "OK");
        }
        catch (InvalidOperationException exception)
        {
            await CurrentPage.DisplayAlertAsync("Erreur", exception.Message, "OK");
        }
        finally
        {
            if (loadVersion == _loadVersion)
                IsBusy = false;
        }
    }

    private async Task ChangeMonthAsync(int monthDelta)
    {
        var newMonth = DisplayedMonth.AddMonths(monthDelta);
        _monthLogs.Clear();
        DisplayedMonth = newMonth;
        SelectedDate = new DateTime(newMonth.Year, newMonth.Month, 1);
        UpdateSelectedDay();
        await LoadAsync();
    }

    private async Task SelectTodayAsync()
    {
        SelectedDate = DateTime.Today;
        if (DisplayedMonth.Year != SelectedDate.Year || DisplayedMonth.Month != SelectedDate.Month)
        {
            _monthLogs.Clear();
            DisplayedMonth = SelectedDate;
            UpdateSelectedDay();
            await LoadAsync();
        }
    }

    private async Task SelectDayAsync(CalendarDay? day)
    {
        if (day == null)
            return;

        if (!day.IsCurrentMonth)
            return;

        SelectedDate = day.Date;
    }

    private void BuildCalendarDays()
    {
        var completedDates = _monthLogs
            .Where(log => log.IsCompleted)
            .Select(log => log.LogDate.Date)
            .ToHashSet();
        var firstDay = new DateTime(DisplayedMonth.Year, DisplayedMonth.Month, 1);
        var mondayOffset = ((int)firstDay.DayOfWeek + 6) % 7;
        var dayCount = DateTime.DaysInMonth(DisplayedMonth.Year, DisplayedMonth.Month);
        var weekCount = (mondayOffset + dayCount + 6) / 7;
        var visibleDayCount = weekCount * 7;
        var firstVisibleDate = firstDay.AddDays(-mondayOffset);

        CalendarDays.Clear();
        for (var index = 0; index < visibleDayCount; index++)
        {
            var date = firstVisibleDate.AddDays(index);
            CalendarDays.Add(new CalendarDay(date, date.Month == DisplayedMonth.Month)
            {
                IsSelected = date == SelectedDate,
                HasCompletions = completedDates.Contains(date)
            });
        }
    }

    private void UpdateSelectedDay()
    {
        DailyLogs.Clear();
        var selectedLogs = _monthLogs
            .Where(log => log.IsCompleted && log.LogDate.Date == SelectedDate)
            .ToList();

        foreach (var log in selectedLogs)
            DailyLogs.Add(log);

        IsDailyLogsEmpty = selectedLogs.Count == 0;
        CompletedCount = selectedLogs.Count;
        DailyCo2Saved = Math.Round(selectedLogs.Sum(log => log.Co2ReductionPerAction), 2);
        foreach (var day in CalendarDays)
            day.IsSelected = day.Date == SelectedDate;

        OnPropertyChanged(nameof(IsTodaySelected));
        OnPropertyChanged(nameof(SelectedDateLabel));
    }
}
