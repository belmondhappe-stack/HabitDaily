using SQLite;

namespace HabitDaily.Models
{
    [Table("HabitLogs")]
    public class HabitLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int HabitId { get; set; }

        public int UserId { get; set; }

        public DateTime Date { get; set; }

        public string Status { get; set; } = "À faire";

        public TimeSpan? ScheduledTime { get; set; }

        public TimeSpan? RescheduledTime { get; set; }

        public TimeSpan? ActionTime { get; set; }

        public int ReportCount { get; set; }

        public double Co2Saved { get; set; }

        public DateTime LogDate { get; set; }

        public bool IsCompleted { get; set; } = false;
    }
}