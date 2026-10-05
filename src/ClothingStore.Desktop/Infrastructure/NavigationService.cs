using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace ClothingStore.Desktop.Infrastructure;

public interface INavigationService
{
    IPageViewModel? CurrentPage { get; }
    Task NavigateToAsync<TPage>(Func<TPage, Task>? initialize = null) where TPage : IPageViewModel;
    event EventHandler? Navigated;
}

public sealed partial class NavigationService(IServiceProvider services) : ObservableObject, INavigationService
{
    [ObservableProperty]
    public partial IPageViewModel? CurrentPage { get; private set; }

    public event EventHandler? Navigated;

    public async Task NavigateToAsync<TPage>(Func<TPage, Task>? initialize = null) where TPage : IPageViewModel
    {
        var page = services.GetRequiredService<TPage>();
        CurrentPage = page;
        Navigated?.Invoke(this, EventArgs.Empty);
        await page.OnNavigatedToAsync();
        if (initialize is not null) await initialize(page);
    }

    public void Reset() => CurrentPage = null;
}
