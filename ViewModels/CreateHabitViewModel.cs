using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using HabitDaily.Constants;
using HabitDaily.Models;
using HabitDaily.Services;
using SQLite;

namespace HabitDaily.ViewModels;

public class CreateHabitViewModel : BaseViewModel
{
    private string _title = string.Empty;
    private string _selectedFrequency = "Quotidienne";
    private HabitColorOption _selectedColor;
    private HabitIconOption _selectedIcon;
    private bool _hasManuallySelectedIcon;
    private bool _isSaving;

    public CreateHabitViewModel()
    {
        AvailableColors = new ObservableCollection<HabitColorOption>
        {
            new("Vert", "#166718"),
            new("Bleu", "#0d638b"),
            new("Orange", "#df6100"),
            new("Rose", "#c8104d"),
            new("Violet", "#361987")
        };

        AvailableIcons = new ObservableCollection<HabitIconOption>
        {
            new("leaf", "Nature", IconFont.Leaf),
            new("book", "Étude", IconFont.Book),
            new("alarm", "Réveil", IconFont.Alarm),
            new("water", "Eau", IconFont.WaterDrop),
            new("power", "Énergie", IconFont.Power)
        };

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

        SelectColorCommand = new Command<string>(SelectColor);
        SelectIconCommand = new Command<HabitIconOption>(SelectIcon);
        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        SaveCommand = new Command(async () => await SaveAsync(), CanSave);
    }

    public ObservableCollection<HabitColorOption> AvailableColors { get; }
    public ObservableCollection<HabitIconOption> AvailableIcons { get; }
    public ObservableCollection<string> Frequencies { get; }

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

        foreach (var option in AvailableColors)
            option.IsSelected = ReferenceEquals(option, color);

        SelectedColor = color;
    }

    private void SelectIcon(HabitIconOption? icon)
    {
        if (icon == null || !AvailableIcons.Contains(icon))
            return;

        _hasManuallySelectedIcon = true;
        SetSelectedIcon(icon);
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

    public HabitIconOption(string key, string label, string glyph)
    {
        Key = key;
        Label = label;
        Glyph = glyph;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }
    public string Label { get; }
    public string Glyph { get; }

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
