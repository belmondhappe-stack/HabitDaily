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

        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public string HexColor { get; set; } = "#00E676";

        public string IconName { get; set; } = "ic_leaf.png";

        [Ignore]
        public string IconGlyph => IconFont.ForHabitTitle(Title, IconName);

        [Ignore]
        public Color IconColor => Color.FromArgb(HexColor);

        public string Frequency { get; set; } = "Quotidienne";

        public string ReminderTime { get; set; } = "08:00";

        public bool IsReminderActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

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