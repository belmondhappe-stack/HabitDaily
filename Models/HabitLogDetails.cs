using HabitDaily.Constants;
using SQLite;

namespace HabitDaily.Models;

public class HabitLogDetails
{
    [PrimaryKey]
    public int Id { get; set; }

    public int HabitId { get; set; }

    public DateTime LogDate { get; set; }

    public bool IsCompleted { get; set; }

    public string HabitTitle { get; set; } = string.Empty;

    public string IconName { get; set; } = string.Empty;

    public string HexColor { get; set; } = "#00E676";

    public double Co2ReductionPerAction { get; set; }

    [Ignore]
    public string IconGlyph => IconFont.ForHabitTitle(HabitTitle, IconName);

    [Ignore]
    public Color IconColor => Color.FromArgb(HexColor);
}
