using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HabitDaily.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected Page CurrentPage
    {
        get
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page
                ?? throw new InvalidOperationException("Aucune fenêtre active n'est disponible.");
            return page is NavigationPage navigationPage
                ? navigationPage.CurrentPage
                : page;
        }
    }

    protected Task NavigateToAsync(Page page)
    {
        var rootPage = Application.Current?.Windows.FirstOrDefault()?.Page
            ?? throw new InvalidOperationException("Aucune fenêtre active n'est disponible.");
        if (rootPage is not NavigationPage navigationPage)
            throw new InvalidOperationException("La navigation de l'application n'est pas initialisée.");

        return navigationPage.Navigation.PushAsync(page);
    }

    protected Task GoBackAsync()
    {
        var rootPage = Application.Current?.Windows.FirstOrDefault()?.Page
            ?? throw new InvalidOperationException("Aucune fenêtre active n'est disponible.");
        if (rootPage is not NavigationPage navigationPage)
            throw new InvalidOperationException("La navigation de l'application n'est pas initialisée.");

        return navigationPage.Navigation.PopAsync();
    }

    protected void SetRootPage(Page page)
    {
        var window = Application.Current?.Windows.FirstOrDefault()
            ?? throw new InvalidOperationException("Aucune fenêtre active n'est disponible.");
        window.Page = page;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
