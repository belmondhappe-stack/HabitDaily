using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HabitDaily.Models;

public sealed class CalendarDay : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _hasCompletions;

    public CalendarDay(DateTime date, bool isCurrentMonth)
    {
        Date = date.Date;
        IsCurrentMonth = isCurrentMonth;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DateTime Date { get; }

    public string DayNumber => IsCurrentMonth ? Date.Day.ToString() : string.Empty;

    public bool IsCurrentMonth { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(BackgroundColor));
                OnPropertyChanged(nameof(TextColor));
                OnPropertyChanged(nameof(StrokeColor));
                OnPropertyChanged(nameof(IndicatorColor));
                OnPropertyChanged(nameof(AccessibilityDescription));
            }
        }
    }

    public bool HasCompletions
    {
        get => _hasCompletions;
        set
        {
            if (SetProperty(ref _hasCompletions, value))
            {
                OnPropertyChanged(nameof(BackgroundColor));
                OnPropertyChanged(nameof(TextColor));
                OnPropertyChanged(nameof(AccessibilityDescription));
            }
        }
    }

    public Color BackgroundColor => !IsCurrentMonth
        ? Colors.Transparent
        : HasCompletions
            ? Color.FromArgb("#00C978")
            : Color.FromArgb("#3B3B3B");

    public Color TextColor => !IsCurrentMonth
        ? Colors.Transparent
        : HasCompletions
            ? Color.FromArgb("#101A14")
            : Color.FromArgb("#FFFFFF");

    public Color StrokeColor => IsSelected
        ? Color.FromArgb("#FFFFFF")
        : Colors.Transparent;

    public Color IndicatorColor => HasCompletions
        ? Color.FromArgb("#101A14")
        : Color.FromArgb("#00E676");

    public string AccessibilityDescription => !IsCurrentMonth
        ? "Jour hors du mois affiché"
        : $"{Date.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR"))}" +
        (HasCompletions ? ", écogeste(s) validé(s)" : string.Empty) +
        (IsSelected ? ", date sélectionnée" : string.Empty);

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
