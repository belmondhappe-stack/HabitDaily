namespace HabitDaily;

public partial class MainPage : ContentPage
{
	int count = 0;
	readonly Button counterButton;

	public MainPage()
	{
		counterButton = new Button { Text = "Click me" };
		counterButton.Clicked += OnCounterClicked;
		Content = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			Children = { counterButton }
		};
	}

	private void OnCounterClicked(object? sender, EventArgs e)
	{
		count++;

		if (count == 1)
			counterButton.Text = $"Clicked {count} time";
		else
			counterButton.Text = $"Clicked {count} times";

		SemanticScreenReader.Announce(counterButton.Text);
	}
}
