using System.ComponentModel;
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

    public string DayNumber => Date.Day.ToString();

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
                OnPropertyChanged(nameof(AccessibilityDescription));
        }
    }

    public Color BackgroundColor => IsSelected
        ? Color.FromArgb("#00E676")
        : Color.FromArgb("#1E1E1E");

    public Color TextColor => !IsCurrentMonth
        ? Color.FromArgb("#757575")
        : IsSelected
            ? Color.FromArgb("#121212")
            : Color.FromArgb("#FFFFFF");

    public Color IndicatorColor => IsSelected
        ? Color.FromArgb("#121212")
        : Color.FromArgb("#00E676");

    public string AccessibilityDescription => $"{Date:dd MMMM yyyy}" +
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
