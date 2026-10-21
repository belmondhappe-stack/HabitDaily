using SQLite;

namespace HabitDaily.Models
{
    [Table("UserProfileType")]
    public class UserProfileType
    {
        [PrimaryKey]
        public string Code { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string SuggestedHabitsJson { get; set; } = "[]";
    }
}