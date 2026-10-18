namespace HabitDaily;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(Views.CreateHabitPage), typeof(Views.CreateHabitPage));
	}
}
