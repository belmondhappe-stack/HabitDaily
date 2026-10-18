using System.Collections.ObjectModel;

namespace HabitDaily.Models;

public sealed class CalendarWeek
{
    public CalendarWeek(IEnumerable<CalendarDay> days) =>
        Days = new ObservableCollection<CalendarDay>(days);

    public ObservableCollection<CalendarDay> Days { get; }
}
