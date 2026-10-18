namespace HabitDaily.Views;

public class StatsPage : ContentPage
{
    public StatsPage() => Content = PlaceholderPageContent.Create("Statistiques", Constants.IconFont.Stats);
}

public class ProfilePage : ContentPage
{
    public ProfilePage() => Content = PlaceholderPageContent.Create("Profil", Constants.IconFont.Profile);
}

internal static class PlaceholderPageContent
{
    public static VerticalStackLayout Create(string title, string icon) => new()
    {
        Padding = 24,
        Spacing = 12,
        VerticalOptions = LayoutOptions.Center,
        Children =
        {
            new Label
            {
                Text = icon,
                FontFamily = "MaterialIcons",
                FontSize = 40,
                TextColor = Application.Current?.Resources["PrimaryGreen"] as Color,
                HorizontalTextAlignment = TextAlignment.Center
            },
            new Label
            {
                Text = title,
                FontSize = 24,
                HorizontalTextAlignment = TextAlignment.Center
            },
            new Label
            {
                Text = "Cette section sera disponible prochainement.",
                HorizontalTextAlignment = TextAlignment.Center
            }
        }
    };
}
