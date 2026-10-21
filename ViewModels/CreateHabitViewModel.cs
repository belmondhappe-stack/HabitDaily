using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using HabitDaily.Constants;
using HabitDaily.Models;
using HabitDaily.Services;
using HabitDaily.Views;
using SQLite;

namespace HabitDaily.ViewModels;

public class CreateHabitViewModel : BaseViewModel
{
    private string _title = string.Empty;
    private string _selectedFrequency = "Quotidienne";
    private Category _selectedCategory = Category.DefaultCategories[^1];
    private HabitColorOption _selectedColor;
    private HabitIconOption _selectedIcon;
    private Color _pickerColor = Color.FromArgb("#166718");
    private bool _hasManuallySelectedIcon;
    private bool _isSaving;

    public CreateHabitViewModel()
    {
        AvailableColors = new ObservableCollection<HabitColorOption>
        {
            new("Vert", "#00E676"),
            new("Bleu", "#29B6F6"),
            new("Orange", "#FFB74D"),
            new("Rose", "#F06292"),
            new("Violet", "#BA68C8")
        };

        AvailableIcons = CreateIconCatalog();

        Frequencies = new ObservableCollection<string>
        {
            "Quotidienne",
            "Hebdomadaire",
            "Jours de semaine",
            "Week-ends"
        };

        _selectedColor = AvailableColors[0];
        _selectedColor.IsSelected = true;
        _selectedIcon = AvailableIcons[0];
        _selectedIcon.IsSelected = true;
        PickerColor = Microsoft.Maui.Graphics.Color.FromArgb(_selectedCategory.Color);

        SelectColorCommand = new Command<string>(SelectColor);
        ChooseIconCommand = new Command(async () => await ChooseIconAsync());
        SelectIconCommand = new Command<HabitIconOption>(SelectIcon);
        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        SaveCommand = new Command(async () => await SaveAsync(), CanSave);
    }

    public ObservableCollection<HabitColorOption> AvailableColors { get; }
    public ObservableCollection<HabitIconOption> AvailableIcons { get; }
    public IReadOnlyList<string> IconCategories { get; } = new[]
    {
        "Nature & écologie",
        "Alimentation",
        "Travail & études",
        "Maison & tâches",
        "Déplacements",
        "Bien-être",
        "Loisirs & quotidien"
    };
    public ObservableCollection<string> Frequencies { get; }
    public IReadOnlyList<Category> Categories => Category.DefaultCategories;

    public string Title
    {
        get => _title;
        set
        {
            if (!SetProperty(ref _title, value))
                return;

            if (!_hasManuallySelectedIcon)
                SelectIconByTitle(value);

            SaveCommand.ChangeCanExecute();
        }
    }

    public string SelectedFrequency
    {
        get => _selectedFrequency;
        set
        {
            if (SetProperty(ref _selectedFrequency, value))
                SaveCommand.ChangeCanExecute();
        }
    }

    public Category SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value))
                return;

            OnPropertyChanged(nameof(DefaultCo2));
            PickerColor = Microsoft.Maui.Graphics.Color.FromArgb(value.Color);
        }
    }

    public double DefaultCo2 => SelectedCategory.DefaultCo2;

    public HabitColorOption SelectedColor
    {
        get => _selectedColor;
        private set
        {
            if (SetProperty(ref _selectedColor, value))
                OnPropertyChanged(nameof(SelectedColorValue));
        }
    }

    public string SelectedColorValue => SelectedColor.HexValue;

    public Color PickerColor
    {
        get => _pickerColor;
        set
        {
            if (!SetProperty(ref _pickerColor, value))
                return;

            SelectColorFromPicker(value);
        }
    }

    public HabitIconOption SelectedIcon
    {
        get => _selectedIcon;
        private set => SetProperty(ref _selectedIcon, value);
    }

    public string SaveButtonText => IsSaving ? "Enregistrement..." : "Créer mon habitude";

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (!SetProperty(ref _isSaving, value))
                return;

            OnPropertyChanged(nameof(SaveButtonText));
            SaveCommand.ChangeCanExecute();
        }
    }

    public ICommand SelectColorCommand { get; }
    public ICommand SelectIconCommand { get; }
    public ICommand ChooseIconCommand { get; }
    public ICommand CancelCommand { get; }
    public Command SaveCommand { get; }

    private bool CanSave() =>
        !IsSaving
        && !string.IsNullOrWhiteSpace(Title)
        && !string.IsNullOrWhiteSpace(SelectedFrequency);

    private void SelectColor(string? hexValue)
    {
        var color = AvailableColors.FirstOrDefault(option => option.HexValue == hexValue);
        if (color == null)
            return;

        PickerColor = color.Color;
    }

    private void SelectColorFromPicker(Color color)
    {
        var hexValue = $"#{(byte)Math.Round(color.Red * 255):X2}{(byte)Math.Round(color.Green * 255):X2}{(byte)Math.Round(color.Blue * 255):X2}";
        var preset = AvailableColors.FirstOrDefault(option =>
            string.Equals(option.HexValue, hexValue, StringComparison.OrdinalIgnoreCase));
        var selectedColor = preset ?? new HabitColorOption("Personnalisée", hexValue);

        foreach (var option in AvailableColors)
            option.IsSelected = ReferenceEquals(option, preset);

        SelectedColor = selectedColor;
    }

    private void SelectIcon(HabitIconOption? icon)
    {
        if (icon == null || !AvailableIcons.Contains(icon))
            return;

        _hasManuallySelectedIcon = true;
        SetSelectedIcon(icon);
    }

    private async Task ChooseIconAsync()
    {
        var picker = new IconPickerPage(AvailableIcons, SelectedIcon);
        await Shell.Current.Navigation.PushModalAsync(picker);
        var selectedIcon = await picker.Selection;
        if (selectedIcon != null)
            SelectIcon(selectedIcon);
    }

    private void SelectIconByTitle(string title)
    {
        var glyph = IconFont.ForHabitTitle(title);
        var icon = AvailableIcons.FirstOrDefault(option => option.Glyph == glyph) ?? AvailableIcons[0];
        SetSelectedIcon(icon);
    }

    private void SetSelectedIcon(HabitIconOption icon)
    {
        foreach (var option in AvailableIcons)
            option.IsSelected = ReferenceEquals(option, icon);

        SelectedIcon = icon;
    }

    private static ObservableCollection<HabitIconOption> CreateIconCatalog()
    {
        var icons = new[]
        {
            new HabitIconOption("leaf", "Feuille", IconFont.Leaf, "Nature & écologie"),
            new HabitIconOption("eco", "Écologie", IconFont.Leaf, "Nature & écologie"),
            new HabitIconOption("recycling", "Recyclage", IconFont.Recycling, "Nature & écologie"),
            new HabitIconOption("compost", "Compost", IconFont.Compost, "Nature & écologie"),
            new HabitIconOption("energy_savings_leaf", "Économie d'énergie", IconFont.EnergySavingsLeaf, "Nature & écologie"),
            new HabitIconOption("electric_bolt", "Électricité", IconFont.ElectricBolt, "Nature & écologie"),
            new HabitIconOption("solar_power", "Solaire", IconFont.SolarPower, "Nature & écologie"),
            new HabitIconOption("park", "Parc", IconFont.Park, "Nature & écologie"),
            new HabitIconOption("forest", "Forêt", IconFont.Forest, "Nature & écologie"),
            new HabitIconOption("local_florist", "Fleurs", IconFont.LocalFlorist, "Nature & écologie"),
            new HabitIconOption("restaurant", "Repas", IconFont.Restaurant, "Alimentation"),
            new HabitIconOption("restaurant_menu", "Menu", IconFont.Restaurant, "Alimentation"),
            new HabitIconOption("lunch_dining", "Déjeuner", IconFont.LunchDining, "Alimentation"),
            new HabitIconOption("bakery_dining", "Boulangerie", IconFont.BakeryDining, "Alimentation"),
            new HabitIconOption("egg", "Cuisine", IconFont.Egg, "Alimentation"),
            new HabitIconOption("local_cafe", "Boisson chaude", IconFont.LocalCafe, "Alimentation"),
            new HabitIconOption("local_drink", "Hydratation", IconFont.LocalDrink, "Alimentation"),
            new HabitIconOption("local_grocery_store", "Courses", IconFont.LocalGroceryStore, "Alimentation"),
            new HabitIconOption("book", "Lecture", IconFont.Book, "Travail & études"),
            new HabitIconOption("menu_book", "Étude", IconFont.Book, "Travail & études"),
            new HabitIconOption("computer", "Ordinateur", IconFont.Computer, "Travail & études"),
            new HabitIconOption("assignment", "Devoirs", IconFont.Assignment, "Travail & études"),
            new HabitIconOption("calculate", "Calcul", IconFont.Calculate, "Travail & études"),
            new HabitIconOption("engineering", "Projet", IconFont.Engineering, "Travail & études"),
            new HabitIconOption("backpack", "École", IconFont.Backpack, "Travail & études"),
            new HabitIconOption("home", "Maison", IconFont.Home, "Maison & tâches"),
            new HabitIconOption("cleaning_services", "Ménage", IconFont.CleaningServices, "Maison & tâches"),
            new HabitIconOption("local_laundry_service", "Lessive", IconFont.LocalLaundryService, "Maison & tâches"),
            new HabitIconOption("kitchen", "Cuisine", IconFont.Kitchen, "Maison & tâches"),
            new HabitIconOption("bed", "Sommeil", IconFont.Bed, "Maison & tâches"),
            new HabitIconOption("brush", "Nettoyage", IconFont.Brush, "Maison & tâches"),
            new HabitIconOption("alarm", "Réveil", IconFont.Alarm, "Maison & tâches"),
            new HabitIconOption("directions_walk", "Marche", IconFont.DirectionsWalk, "Déplacements"),
            new HabitIconOption("directions_bike", "Vélo", IconFont.DirectionsBike, "Déplacements"),
            new HabitIconOption("directions_bus", "Bus", IconFont.DirectionsBus, "Déplacements"),
            new HabitIconOption("directions_car", "Voiture", IconFont.DirectionsCar, "Déplacements"),
            new HabitIconOption("train", "Train", IconFont.Train, "Déplacements"),
            new HabitIconOption("electric_car", "Voiture électrique", IconFont.ElectricCar, "Déplacements"),
            new HabitIconOption("directions_run", "Course", IconFont.DirectionsRun, "Bien-être"),
            new HabitIconOption("fitness_center", "Sport", IconFont.FitnessCenter, "Bien-être"),
            new HabitIconOption("self_improvement", "Méditation", IconFont.SelfImprovement, "Bien-être"),
            new HabitIconOption("medication", "Santé", IconFont.Medication, "Bien-être"),
            new HabitIconOption("favorite", "Bien-être", IconFont.Favorite, "Bien-être"),
            new HabitIconOption("shower", "Douche", IconFont.Shower, "Bien-être"),
            new HabitIconOption("pets", "Animaux", IconFont.Pets, "Loisirs & quotidien"),
            new HabitIconOption("lightbulb", "Idée", IconFont.Lightbulb, "Loisirs & quotidien"),
            new HabitIconOption("shopping_cart", "Achats", IconFont.ShoppingCart, "Loisirs & quotidien"),
            new HabitIconOption("savings", "Économies", IconFont.Savings, "Loisirs & quotidien"),
            new HabitIconOption("power", "Énergie", IconFont.Power, "Loisirs & quotidien"),
            new HabitIconOption("spa", "Détente", IconFont.Spa, "Loisirs & quotidien"),
            new HabitIconOption("water_drop", "Eau", IconFont.WaterDrop, "Loisirs & quotidien")
        };

        return new ObservableCollection<HabitIconOption>(icons);
    }

    private async Task SaveAsync()
    {
        if (!CanSave())
        {
            await CurrentPage.DisplayAlertAsync(
                "Informations manquantes",
                "Saisissez un nom et choisissez une fréquence pour votre habitude.",
                "OK");
            return;
        }

        IsSaving = true;
        try
        {
            var user = await DatabaseService.GetCurrentUserAsync();
            if (user == null)
            {
                await CurrentPage.DisplayAlertAsync(
                    "Session introuvable",
                    "Connectez-vous avant de créer une habitude.",
                    "OK");
                return;
            }

            var habit = new Habit
            {
                UserId = user.Id,
                Title = Title.Trim(),
                Category = SelectedCategory.Code,
                Color = SelectedColor.HexValue,
                Icon = SelectedIcon.Key,
                FrequencyType = SelectedFrequency,
                Co2ReductionPerAction = SelectedCategory.DefaultCo2,
                Subtitle = SelectedFrequency,
                HexColor = SelectedColor.HexValue,
                IconName = SelectedIcon.Key,
                Frequency = SelectedFrequency,
                IsReminderActive = false
            };

            await DatabaseService.SaveHabitAsync(habit);
            await Shell.Current.GoToAsync("..");
        }
        catch (SQLiteException exception)
        {
            await CurrentPage.DisplayAlertAsync(
                "Enregistrement impossible",
                $"L'habitude n'a pas pu être enregistrée : {exception.Message}",
                "OK");
        }
        finally
        {
            IsSaving = false;
        }
    }
}

public sealed class HabitColorOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public HabitColorOption(string name, string hexValue)
    {
        Name = name;
        HexValue = hexValue;
        Color = Color.FromArgb(hexValue);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }
    public string HexValue { get; }
    public Color Color { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}

public sealed class HabitIconOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public HabitIconOption(string key, string label, string glyph, string category = "")
    {
        Key = key;
        Label = label;
        Glyph = glyph;
        Category = category;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }
    public string Label { get; }
    public string Glyph { get; }
    public string Category { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
