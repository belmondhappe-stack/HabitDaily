using SQLite;

namespace HabitDaily.Models
{
    [Table("User")]
    public class User
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public int CurrentStreak { get; set; } = 0;

        public int BestStreak { get; set; } = 0;

        public double SuccessRate { get; set; } = 0.0;

        public double TotalCo2Saved { get; set; } = 0.0;

        public string LevelBadge { get; set; } = "Éco-championne · Niv. 1";
    }
}