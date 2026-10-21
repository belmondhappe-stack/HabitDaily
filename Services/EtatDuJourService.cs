using HabitDaily.Models;

namespace HabitDaily.Services;

public sealed class EtatDuJourService
{
    private const double ResponseWindowMinutes = 60;
    private const double MinimumScheduleGapMinutes = 30;
    private const int MaximumReportsPerDay = 2;
    private readonly Func<DateTime> _clock;

    public EtatDuJourService(Func<DateTime>? clock = null)
    {
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task EvaluateHabitsForUserAsync(int userId, DateTime targetDate)
    {
        var habits = await DatabaseService.GetHabitsAsync(userId);
        foreach (var habit in habits)
            await EvaluateHabitStatusAsync(habit, targetDate);
    }

    public async Task RunEngineCatchUpAsync(int userId)
    {
        var habits = await DatabaseService.GetHabitsAsync(userId);
        var today = _clock().Date;
        if (habits.Count == 0)
            return;

        var preferenceKey = $"EtatDuJourLastRun_{userId}";
        var lastRunBinary = Preferences.Get(preferenceKey, 0L);
        var nextCatchUpDate = lastRunBinary == 0
            ? (DateTime?)null
            : DateTime.FromBinary(lastRunBinary).Date.AddDays(1);

        foreach (var habit in habits)
        {
            var firstDate = MaxDate(habit.StartDate.Date, habit.CreatedAt.Date);
            if (nextCatchUpDate.HasValue)
                firstDate = MaxDate(firstDate, nextCatchUpDate.Value);

            for (var date = firstDate; date < today; date = date.AddDays(1))
                await EvaluateHabitStatusAsync(habit, date);
        }

        await EvaluateHabitsForUserAsync(userId, today);
        Preferences.Set(preferenceKey, today.ToBinary());
    }

    /// <summary>
    /// Returns null when the habit has no occurrence on the requested date.
    /// </summary>
    public async Task<string?> EvaluateHabitStatusAsync(Habit habit, DateTime targetDate)
    {
        ArgumentNullException.ThrowIfNull(habit);

        var date = targetDate.Date;
        var now = _clock();
        if (date < habit.StartDate.Date || (habit.EndDate.HasValue && date > habit.EndDate.Value.Date))
            return "Hors période";

        var log = await DatabaseService.GetLogForDateAsync(habit.Id, date);
        if (log?.IsCompleted == true)
            return log.Status is "Faite" or "Faite en retard" ? log.Status : "Faite";
        if (log?.Status == "Annulée")
            return "Annulée";
        if (log?.Status == "Manquée")
            return "Manquée";

        if (IsQuotaFrequency(habit))
            return await EvaluateQuotaStatusAsync(habit, date, now.Date);

        var firstOccurrence = GetFirstOccurrenceDate(habit);
        if (!firstOccurrence.HasValue || date < firstOccurrence.Value)
            return "Hors période";

        if (!OccursOnDate(habit, date))
            return null;

        var scheduledTime = log?.RescheduledTime ?? log?.ScheduledTime ?? habit.ScheduledTime;
        if (date > now.Date)
            return log?.Status == "Reportée" ? "Reportée" : "À venir";

        if (date < now.Date)
            return await MarkMissedAsync(habit, date, log, scheduledTime);

        if (scheduledTime.HasValue && log?.Status == "Reportée" && now.TimeOfDay < scheduledTime.Value)
            return "Reportée";

        if (scheduledTime.HasValue && now.TimeOfDay < scheduledTime.Value)
            return "À venir";

        if (!scheduledTime.HasValue)
            return "À faire";

        if (log?.Status == "Reportée")
        {
            log.Status = "À faire";
            await DatabaseService.SaveHabitLogAsync(log);
        }

        var deadline = await GetResponseDeadlineAsync(habit, date, scheduledTime.Value);
        if (now.TimeOfDay >= deadline)
            return await MarkMissedAsync(habit, date, log, scheduledTime);

        return "À faire";
    }

    public async Task<bool> CompleteHabitAsync(int habitId, DateTime date)
    {
        var now = _clock();
        if (date.Date != now.Date || now.TimeOfDay > new TimeSpan(23, 59, 0))
            return false;

        var habit = await DatabaseService.GetHabitByIdAsync(habitId);
        if (habit == null || habit.IsArchived)
            return false;

        var status = await EvaluateHabitStatusAsync(habit, date);
        if (status is null or "Hors période" or "Quota atteint")
            return false;

        var log = await DatabaseService.GetLogForDateAsync(habitId, date);
        if (log?.IsCompleted == true)
            return true;

        await DatabaseService.SaveHabitCompletionAsync(
            habit.UserId,
            habitId,
            true,
            date,
            now.TimeOfDay,
            now);
        return true;
    }

    public async Task<bool> RescheduleHabitAsync(int habitId, DateTime date, TimeSpan newTime)
    {
        var now = _clock();
        if (date.Date != now.Date
            || newTime < now.TimeOfDay.Add(TimeSpan.FromMinutes(10))
            || newTime > new TimeSpan(23, 30, 0)
            || newTime.Ticks % TimeSpan.FromMinutes(15).Ticks != 0)
        {
            return false;
        }

        var habit = await DatabaseService.GetHabitByIdAsync(habitId);
        if (habit == null || habit.IsArchived || !habit.ScheduledTime.HasValue)
            return false;

        var existingLog = await DatabaseService.GetLogForDateAsync(habitId, date);
        var effectiveScheduledTime = existingLog?.RescheduledTime
            ?? existingLog?.ScheduledTime
            ?? habit.ScheduledTime.Value;
        if (now.TimeOfDay < effectiveScheduledTime.Add(TimeSpan.FromMinutes(10)))
            return false;

        var status = await EvaluateHabitStatusAsync(habit, date);
        if (status != "À faire")
            return false;

        var log = existingLog ?? new HabitLog
        {
            HabitId = habitId,
            UserId = habit.UserId,
            Date = date.Date,
            LogDate = date.Date,
            ScheduledTime = habit.ScheduledTime
        };
        if (log.ReportCount >= MaximumReportsPerDay)
            return false;

        if (await HasScheduleConflictAsync(habit.UserId, date, newTime, habitId))
            return false;

        log.Status = "Reportée";
        log.RescheduledTime = newTime;
        log.ActionTime = now.TimeOfDay;
        log.ReportCount++;
        log.IsCompleted = false;
        log.Co2Saved = 0;
        await DatabaseService.SaveHabitLogAsync(log);
        return true;
    }

    public async Task<bool> CancelHabitForDayAsync(int habitId, DateTime date)
    {
        if (date.Date != _clock().Date)
            return false;

        var habit = await DatabaseService.GetHabitByIdAsync(habitId);
        if (habit == null || habit.IsArchived)
            return false;

        var status = await EvaluateHabitStatusAsync(habit, date);
        if (status != "À faire")
            return false;

        var log = await DatabaseService.GetLogForDateAsync(habitId, date) ?? new HabitLog
        {
            HabitId = habitId,
            UserId = habit.UserId,
            Date = date.Date,
            LogDate = date.Date,
            ScheduledTime = habit.ScheduledTime
        };
        log.Status = "Annulée";
        log.IsCompleted = false;
        log.Co2Saved = 0;
        log.ActionTime = _clock().TimeOfDay;
        await DatabaseService.SaveHabitLogAsync(log);
        return true;
    }

    public async Task<List<Habit>> GetHabitsDueTodayAsync(int userId)
    {
        var today = _clock().Date;
        var habits = await DatabaseService.GetHabitsAsync(userId);
        var dueHabits = new List<Habit>();

        foreach (var habit in habits)
        {
            if (today < habit.StartDate.Date
                || (habit.EndDate.HasValue && today > habit.EndDate.Value.Date)
                || (!IsQuotaFrequency(habit) && !OccursOnDate(habit, today)))
            {
                continue;
            }

            var status = await EvaluateHabitStatusAsync(habit, today);
            if (status is null or "Hors période" or "Quota atteint")
                continue;

            var log = await DatabaseService.GetLogForDateAsync(habit.Id, today);
            if (log?.IsCompleted == true)
                continue;

            dueHabits.Add(habit);
        }

        return dueHabits;
    }

    public async Task<int> GetCurrentQuotaStreakWeeksAsync(int habitId)
    {
        var habit = await DatabaseService.GetHabitByIdAsync(habitId);
        if (habit == null || !IsWeeklyQuota(habit))
            return 0;

        var logs = await DatabaseService.GetHabitLogsForHabitAsync(habitId);
        var today = _clock().Date;
        var (currentWeekStart, _, _) = GetQuotaPeriod(habit, today);
        var activeStart = MaxDate(habit.StartDate.Date, habit.CreatedAt.Date);
        var streak = 0;

        for (var weekStart = currentWeekStart; weekStart >= activeStart; weekStart = weekStart.AddDays(-7))
        {
            var periodEnd = weekStart.AddDays(6);
            var activeDays = CountActivePeriodDays(habit, weekStart, periodEnd, 7);
            if (activeDays == 0)
                continue;

            var required = GetRequiredQuotaCount(habit, activeDays, 7);
            var completed = logs.Count(log => log.IsCompleted
                && log.Date.Date >= weekStart
                && log.Date.Date <= periodEnd);

            if (weekStart == currentWeekStart && completed < required)
                continue;
            if (completed < required)
                break;

            streak++;
        }

        return streak;
    }

    public static (int Current, int Best) CalculateDailyStreaks(
        IReadOnlyCollection<Habit> habits,
        IReadOnlyCollection<HabitLog> completedLogs,
        DateTime today)
    {
        var dailyHabits = habits.Where(habit => !IsQuotaFrequency(habit)).ToList();
        if (dailyHabits.Count == 0)
            return (0, 0);

        var firstDate = dailyHabits.Min(habit => MaxDate(habit.StartDate.Date, habit.CreatedAt.Date));
        var completed = completedLogs
            .Where(log => log.IsCompleted)
            .Select(log => (log.HabitId, Date: log.Date.Date))
            .ToHashSet();

        var current = 0;
        for (var date = today.Date; date >= firstDate; date = date.AddDays(-1))
        {
            var dueHabits = dailyHabits.Where(habit => IsActiveOnDate(habit, date) && OccursOnDate(habit, date));
            if (!dueHabits.Any())
                continue;

            if (dueHabits.Any(habit => completed.Contains((habit.Id, date))))
            {
                current++;
                continue;
            }

            if (date == today.Date)
                continue;

            break;
        }

        var best = 0;
        var run = 0;
        for (var date = firstDate; date <= today.Date; date = date.AddDays(1))
        {
            var dueHabits = dailyHabits.Where(habit => IsActiveOnDate(habit, date) && OccursOnDate(habit, date));
            if (!dueHabits.Any())
                continue;

            if (dueHabits.Any(habit => completed.Contains((habit.Id, date))))
            {
                run++;
                best = Math.Max(best, run);
            }
            else if (date != today.Date)
            {
                run = 0;
            }
        }

        return (current, best);
    }

    /// <summary>
    /// Checks conflicts only against non-archived habits scheduled on the same date.
    /// </summary>
    public async Task<bool> HasScheduleConflictAsync(
        int userId,
        DateTime date,
        TimeSpan newHabitTime,
        int? excludeHabitId = null)
    {
        if (newHabitTime < TimeSpan.Zero || newHabitTime >= TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(newHabitTime));

        var targetDate = date.Date;
        var habits = await DatabaseService.GetHabitsAsync(userId);
        foreach (var habit in habits)
        {
            if (habit.Id == excludeHabitId
                || targetDate < habit.StartDate.Date
                || (habit.EndDate.HasValue && targetDate > habit.EndDate.Value.Date)
                || !OccursOnDate(habit, targetDate))
            {
                continue;
            }

            var log = await DatabaseService.GetLogForDateAsync(habit.Id, targetDate);
            var scheduledTime = log?.RescheduledTime ?? log?.ScheduledTime ?? habit.ScheduledTime;
            if (scheduledTime.HasValue
                && Math.Abs((newHabitTime - scheduledTime.Value).TotalMinutes) < MinimumScheduleGapMinutes)
                return true;
        }

        return false;
    }

    public async Task<DateTime?> FindFirstScheduleConflictDateAsync(Habit candidate, int? excludeHabitId = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!candidate.ScheduledTime.HasValue || IsQuotaFrequency(candidate))
            return null;

        var firstDate = GetFirstOccurrenceDate(candidate);
        if (!firstDate.HasValue)
            return null;

        for (var offset = 0; offset < 14; offset++)
        {
            var date = firstDate.Value.AddDays(offset);
            if (candidate.EndDate.HasValue && date > candidate.EndDate.Value.Date)
                break;
            if (!OccursOnDate(candidate, date))
                continue;

            if (await HasScheduleConflictAsync(
                    candidate.UserId,
                    date,
                    candidate.ScheduledTime.Value,
                    excludeHabitId))
            {
                return date;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the next 15-minute slot on the requested day that has no schedule conflict.
    /// </summary>
    public async Task<TimeSpan?> FindNextAvailableTimeAsync(
        int userId,
        DateTime date,
        TimeSpan preferredTime,
        int? excludeHabitId = null)
    {
        var firstMinute = Math.Max(30, (int)Math.Ceiling(preferredTime.TotalMinutes / 15) * 15);
        for (var minute = firstMinute; minute <= 23 * 60 + 30; minute += 15)
        {
            var candidate = TimeSpan.FromMinutes(minute);
            if (!await HasScheduleConflictAsync(userId, date, candidate, excludeHabitId))
                return candidate;
        }

        return null;
    }

    private async Task<TimeSpan> GetResponseDeadlineAsync(Habit habit, DateTime date, TimeSpan scheduledTime)
    {
        var deadline = scheduledTime.Add(TimeSpan.FromMinutes(ResponseWindowMinutes));
        var endOfDay = new TimeSpan(23, 59, 0);
        if (deadline > endOfDay)
            deadline = endOfDay;

        var habits = await DatabaseService.GetHabitsAsync(habit.UserId);
        foreach (var nextHabit in habits)
        {
            if (nextHabit.Id == habit.Id
                || !OccursOnDate(nextHabit, date)
                || date < nextHabit.StartDate.Date
                || (nextHabit.EndDate.HasValue && date > nextHabit.EndDate.Value.Date))
            {
                continue;
            }

            var nextLog = await DatabaseService.GetLogForDateAsync(nextHabit.Id, date);
            var nextTime = nextLog?.RescheduledTime ?? nextLog?.ScheduledTime ?? nextHabit.ScheduledTime;
            if (nextTime.HasValue && nextTime.Value > scheduledTime && nextTime.Value < deadline)
                deadline = nextTime.Value;
        }

        return deadline;
    }

    private async Task<string> EvaluateQuotaStatusAsync(Habit habit, DateTime date, DateTime today)
    {
        if (date > today)
            return "À venir";

        var (periodStart, periodEnd, periodLength) = GetQuotaPeriod(habit, date);
        var activeStart = MaxDate(periodStart, MaxDate(habit.StartDate.Date, habit.CreatedAt.Date));
        var activeEnd = habit.EndDate.HasValue && habit.EndDate.Value.Date < periodEnd
            ? habit.EndDate.Value.Date
            : periodEnd;
        if (activeStart > activeEnd || date < activeStart)
            return "Hors période";
        if (habit.QuotaCount <= 0)
            return "À faire";

        var activeDays = (activeEnd - activeStart).Days + 1;
        var requiredCount = GetRequiredQuotaCount(habit, activeDays, periodLength);
        var logs = await DatabaseService.GetHabitLogsForHabitAsync(habit.Id);
        var completedCount = logs.Count(log => log.IsCompleted
            && log.Date.Date >= activeStart
            && log.Date.Date <= date
            && log.Date.Date <= activeEnd);

        if (completedCount >= requiredCount)
            return "Quota atteint";

        if (IsWeeklyQuota(habit))
        {
            var daysRemaining = (activeEnd - date).Days + 1;
            if (daysRemaining < requiredCount - completedCount)
                return "Semaine compromise";
        }

        return "À faire";
    }

    private static (DateTime Start, DateTime End, int Length) GetQuotaPeriod(Habit habit, DateTime date)
    {
        if (IsWeeklyQuota(habit))
        {
            var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
            var start = date.Date.AddDays(-daysSinceMonday);
            return (start, start.AddDays(6), 7);
        }

        var monthStart = new DateTime(date.Year, date.Month, 1);
        return (monthStart, monthStart.AddMonths(1).AddDays(-1), DateTime.DaysInMonth(date.Year, date.Month));
    }

    private static int GetRequiredQuotaCount(Habit habit, int activeDays, int periodLength) =>
        habit.QuotaCount <= 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(habit.QuotaCount * activeDays / (double)periodLength));

    private static int CountActivePeriodDays(Habit habit, DateTime periodStart, DateTime periodEnd, int periodLength)
    {
        var activeStart = MaxDate(periodStart, MaxDate(habit.StartDate.Date, habit.CreatedAt.Date));
        var activeEnd = habit.EndDate.HasValue && habit.EndDate.Value.Date < periodEnd
            ? habit.EndDate.Value.Date
            : periodEnd;
        return activeStart > activeEnd ? 0 : Math.Min(periodLength, (activeEnd - activeStart).Days + 1);
    }

    private static bool IsActiveOnDate(Habit habit, DateTime date) =>
        date.Date >= MaxDate(habit.StartDate.Date, habit.CreatedAt.Date)
        && (!habit.EndDate.HasValue || date.Date <= habit.EndDate.Value.Date);

    private async Task<string> MarkMissedAsync(
        Habit habit,
        DateTime date,
        HabitLog? log,
        TimeSpan? scheduledTime)
    {
        log ??= new HabitLog
        {
            HabitId = habit.Id,
            UserId = habit.UserId,
            Date = date,
            LogDate = date
        };

        log.Status = "Manquée";
        log.IsCompleted = false;
        log.ScheduledTime ??= habit.ScheduledTime;
        log.Co2Saved = 0;
        await DatabaseService.SaveHabitLogAsync(log);
        return "Manquée";
    }

    private static DateTime? GetFirstOccurrenceDate(Habit habit)
    {
        var firstCandidate = habit.StartDate.Date > habit.CreatedAt.Date
            ? habit.StartDate.Date
            : habit.CreatedAt.Date;
        if (habit.EndDate.HasValue && firstCandidate > habit.EndDate.Value.Date)
            return null;

        for (var offset = 0; offset <= 366; offset++)
        {
            var candidate = firstCandidate.AddDays(offset);
            if (habit.EndDate.HasValue && candidate > habit.EndDate.Value.Date)
                return null;
            if (!OccursOnDate(habit, candidate))
                continue;

            if (candidate == habit.CreatedAt.Date
                && habit.ScheduledTime.HasValue
                && habit.ScheduledTime.Value < habit.CreatedAt.TimeOfDay.Add(TimeSpan.FromMinutes(15)))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static DateTime MaxDate(DateTime first, DateTime second) =>
        first.Date >= second.Date ? first.Date : second.Date;

    private static string GetFrequencyType(Habit habit) =>
        string.IsNullOrWhiteSpace(habit.FrequencyType)
            ? habit.Frequency?.Trim() ?? string.Empty
            : habit.FrequencyType.Trim();

    private static bool OccursOnDate(Habit habit, DateTime date)
    {
        var type = GetFrequencyType(habit);

        return type switch
        {
            "JoursChoisis" => IsSelectedDay(habit.SelectedDays, date.DayOfWeek),
            "Hebdomadaire" => string.IsNullOrWhiteSpace(habit.SelectedDays)
                ? date.DayOfWeek == DayOfWeek.Monday
                : IsSelectedDay(habit.SelectedDays, date.DayOfWeek),
            "Jours de semaine" => date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday,
            "Week-ends" => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            "TousLesJours" or "Quotidienne" or "Quotidien" => true,
            "QuotaSemaine" or "QuotaMois" => false,
            _ => true
        };
    }

    private static bool IsWeeklyQuota(Habit habit) =>
        GetFrequencyType(habit) is "QuotaSemaine" or "QuotaHebdomadaire";

    private static bool IsQuotaFrequency(Habit habit) =>
        IsWeeklyQuota(habit) || GetFrequencyType(habit) is "QuotaMois" or "QuotaMensuel";

    private static bool IsSelectedDay(string? selectedDays, DayOfWeek dayOfWeek)
    {
        if (string.IsNullOrWhiteSpace(selectedDays))
            return false;

        var expectedToken = dayOfWeek switch
        {
            DayOfWeek.Monday => "L",
            DayOfWeek.Tuesday => "M",
            DayOfWeek.Wednesday => "Me",
            DayOfWeek.Thursday => "J",
            DayOfWeek.Friday => "V",
            DayOfWeek.Saturday => "S",
            _ => "D"
        };

        return selectedDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(token => string.Equals(token, expectedToken, StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, dayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase));
    }
}