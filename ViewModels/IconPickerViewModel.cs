using System.Collections.ObjectModel;
using System.Windows.Input;

namespace HabitDaily.ViewModels;

public sealed class IconPickerViewModel : BaseViewModel
{
    private readonly IReadOnlyList<HabitIconOption> _allIcons;
    private string _searchText = string.Empty;
    private string _selectedCategory = "Toutes";

    public IconPickerViewModel(
        IReadOnlyList<HabitIconOption> icons,
        HabitIconOption selectedIcon)
    {
        _allIcons = icons;
        SelectedIcon = selectedIcon;
        CategoryFilters = new ObservableCollection<string>(
            new[] { "Toutes" }.Concat(icons.Select(icon => icon.Category).Distinct()));
        Categories = new ObservableCollection<HabitIconCategory>();
        SelectIconCommand = new Command<HabitIconOption>(SelectIcon);
        CancelCommand = new Command(async () => await CancelAsync());
        UpdateCategories();
    }

    public ObservableCollection<HabitIconCategory> Categories { get; }
    public ObservableCollection<string> CategoryFilters { get; }
    public ICommand SelectIconCommand { get; }
    public ICommand CancelCommand { get; }

    public HabitIconOption SelectedIcon { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                UpdateCategories();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
                UpdateCategories();
        }
    }

    public event EventHandler<HabitIconOption>? IconSelected;

    private void UpdateCategories()
    {
        var filteredIcons = _allIcons.Where(icon =>
                (SelectedCategory == "Toutes" || icon.Category == SelectedCategory)
                && (string.IsNullOrWhiteSpace(SearchText)
                    || icon.Label.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
                    || icon.Key.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
                    || icon.Category.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();

        Categories.Clear();
        foreach (var grouping in filteredIcons.GroupBy(icon => icon.Category))
            Categories.Add(new HabitIconCategory(grouping.Key, grouping));
    }

    private void SelectIcon(HabitIconOption? icon)
    {
        if (icon != null && _allIcons.Contains(icon))
            IconSelected?.Invoke(this, icon);
    }

    private async Task CancelAsync()
    {
        await Shell.Current.Navigation.PopModalAsync();
    }
}

public sealed class HabitIconCategory : ObservableCollection<HabitIconOption>
{
    public HabitIconCategory(string name, IEnumerable<HabitIconOption> icons)
        : base(icons)
    {
        Name = name;
    }

    public string Name { get; }
}
