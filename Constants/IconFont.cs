namespace HabitDaily.Constants;

public static class IconFont
{
    public const string Home = "\ue88a";
    public const string Calendar = "\ue916";
    public const string Stats = "\ue6e1";
    public const string Profile = "\ue7fd";
    public const string Leaf = "\uea35";
    public const string Plus = "\ue145";
    public const string Check = "\ue5ca";
    public const string Unchecked = "\ue836";
    public const string Fire = "\ue80e";
    public const string Email = "\ue0be";
    public const string Mail = "\ue158";
    public const string Lock = "\ue897";
    public const string Person = "\ue7fd";
    public const string Login = "\uea77";
    public const string PersonAdd = "\ue7fe";
    public const string LockReset = "\ueade";
    public const string ArrowBack = "\ue5c4";
    public const string Book = "\ue865";
    public const string Alarm = "\ue855";
    public const string WaterDrop = "\ue798";
    public const string Power = "\ue8ac";

    public static string ForHabitTitle(string? title, string? iconName = null)
    {
        switch (iconName)
        {
            case "leaf":
                return Leaf;
            case "book":
                return Book;
            case "alarm":
                return Alarm;
            case "water":
                return WaterDrop;
            case "power":
                return Power;
        }

        var normalizedTitle = title?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalizedTitle.Contains("devoir", StringComparison.Ordinal)
            || normalizedTitle.Contains("cahier", StringComparison.Ordinal)
            || normalizedTitle.Contains("étude", StringComparison.Ordinal)
            || normalizedTitle.Contains("etude", StringComparison.Ordinal))
        {
            return Book;
        }

        if (normalizedTitle.Contains("lever", StringComparison.Ordinal)
            || normalizedTitle.Contains("réveil", StringComparison.Ordinal)
            || normalizedTitle.Contains("reveil", StringComparison.Ordinal)
            || normalizedTitle.Contains("matin", StringComparison.Ordinal))
        {
            return Alarm;
        }

        if (normalizedTitle.Contains("arroser", StringComparison.Ordinal)
            || normalizedTitle.Contains("plante", StringComparison.Ordinal)
            || normalizedTitle.Contains("jardin", StringComparison.Ordinal))
        {
            return WaterDrop;
        }

        if (normalizedTitle.Contains("éteindre", StringComparison.Ordinal)
            || normalizedTitle.Contains("eteindre", StringComparison.Ordinal)
            || normalizedTitle.Contains("veille", StringComparison.Ordinal)
            || normalizedTitle.Contains("énergie", StringComparison.Ordinal)
            || normalizedTitle.Contains("energie", StringComparison.Ordinal)
            || normalizedTitle.Contains("électricité", StringComparison.Ordinal)
            || normalizedTitle.Contains("electricite", StringComparison.Ordinal))
        {
            return Power;
        }

        return Leaf;
    }
}