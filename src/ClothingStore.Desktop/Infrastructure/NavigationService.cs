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

    /// <summary>
    /// Pages opened in this sign-in are kept, so going back to one shows it straight away (its data is refreshed by
    /// <see cref="IPageViewModel.OnNavigatedToAsync"/>) instead of building it again.
    /// </summary>
    private readonly Dictionary<Type, IPageViewModel> _pages = [];

    public IPageViewModel GetPage(Type pageType)
    {
        if (!_pages.TryGetValue(pageType, out var page))
            _pages[pageType] = page = (IPageViewModel)services.GetRequiredService(pageType);
        return page;
    }

    public async Task NavigateToAsync<TPage>(Func<TPage, Task>? initialize = null) where TPage : IPageViewModel
    {
        var page = (TPage)GetPage(typeof(TPage));
        CurrentPage = page;
        Navigated?.Invoke(this, EventArgs.Empty);
        await page.OnNavigatedToAsync();
        if (initialize is not null) await initialize(page);
    }

    /// <summary>Signing out or rebuilding the window: forget the pages (another user, another language).</summary>
    public void Reset()
    {
        CurrentPage = null;
        _pages.Clear();
    }
}
