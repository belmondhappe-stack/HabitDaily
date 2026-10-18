using SQLite;

namespace HabitDaily.Models
{
    [Table("HabitLogs")]
    public class HabitLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int HabitId { get; set; }

        public DateTime LogDate { get; set; }

        public bool IsCompleted { get; set; } = false;
    }
}