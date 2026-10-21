using System.ComponentModel;
using HabitDaily.Constants;
using SQLite;

namespace HabitDaily.Models
{
    [Table("Habits")]
    public class Habit : INotifyPropertyChanged
    {
        private bool _isCompletedToday;

        public event PropertyChangedEventHandler? PropertyChanged;

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int UserId { get; set; }

        public int? GoalId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Category { get; set; } = "Autre";

        public string Color { get; set; } = "#00E676";

        public string Icon { get; set; } = "🌱";

        public string Subtitle { get; set; } = string.Empty;

        public string HexColor { get; set; } = "#00E676";

        public string IconName { get; set; } = "ic_leaf.png";

        [Ignore]
        public string IconGlyph => IconFont.ForHabitTitle(Title, IconName);

        [Ignore]
        public Color IconColor => Microsoft.Maui.Graphics.Color.FromArgb(HexColor);

        public string Frequency { get; set; } = "Quotidienne";

        public string FrequencyType { get; set; } = "Quotidienne";

        public string SelectedDays { get; set; } = string.Empty;

        public int QuotaCount { get; set; }

        public TimeSpan? ScheduledTime { get; set; }

        public DateTime StartDate { get; set; } = DateTime.Today;

        public DateTime? EndDate { get; set; }

        public double Co2ReductionPerAction { get; set; } = 0.2;

        public bool IsArchived { get; set; }

        public string ReminderTime { get; set; } = "08:00";

        public bool IsReminderActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Ignore]
        public int CurrentStreak { get; set; }

        [Ignore]
        public bool IsCompletedToday
        {
            get => _isCompletedToday;
            set
            {
                if (_isCompletedToday == value)
                    return;

                _isCompletedToday = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompletedToday)));
            }
        }
    }
}