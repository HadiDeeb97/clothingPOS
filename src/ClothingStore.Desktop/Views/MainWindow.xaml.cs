using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ClothingStore.Desktop.Infrastructure;

namespace ClothingStore.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WindowAppearance.Apply(this);
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => PreparePagesWhenIdle();
    }

    /// <summary>
    /// After the window is up, builds the other pages' screens one at a time whenever the app is idle, so opening any
    /// page for the first time is quick too. Nothing is loaded from the database until a page is opened.
    /// </summary>
    private void PreparePagesWhenIdle()
    {
        if (DataContext is not ViewModels.MainViewModel main
            || App.Services.GetService(typeof(NavigationService)) is not NavigationService navigation) return;
        var queue = new Queue<Type>(main.NavGroups.SelectMany(g => g.Items).Select(i => i.PageType));
        void Next()
        {
            if (queue.Count == 0 || !IsLoaded) return;
            try
            {
                PageHost.Prepare(navigation.GetPage(queue.Dequeue()));
            }
            catch (Exception)
            {
                // Best effort: the page is simply built when it is opened.
            }
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, Next);
        }
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, Next);
    }

    /// <summary>
    /// Shortcuts that work the same on every page: Ctrl+F jumps to the page's search box, F5 refreshes the page.
    /// (The register's own F-keys are handled by the register.)
    /// </summary>
    /// <summary>
    /// A page's own shortcuts (the register's F4–F12) work wherever the keyboard focus is in the window, not only
    /// after clicking inside the page (e.g. right after using the sidebar or the top bar).
    /// </summary>
    private bool RunPageShortcut(KeyEventArgs e)
    {
        if (VisualTreeHelper.GetChildrenCount(PageHost) == 0 || FindView(PageHost) is not { } view) return false;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        foreach (var binding in view.InputBindings.OfType<KeyBinding>())
        {
            if (binding.Key != key || binding.Modifiers != Keyboard.Modifiers || binding.Command is not { } command) continue;
            // Inside the page the binding fires by itself.
            if (view.IsKeyboardFocusWithin) return false;
            if (command.CanExecute(binding.CommandParameter)) command.Execute(binding.CommandParameter);
            return true;
        }
        return false;
    }

    private static UIElement? FindView(DependencyObject host)
    {
        var child = VisualTreeHelper.GetChild(host, 0);
        // ContentControl → ContentPresenter → the page's UserControl.
        while (child is not null and not System.Windows.Controls.UserControl && VisualTreeHelper.GetChildrenCount(child) > 0)
            child = VisualTreeHelper.GetChild(child, 0);
        return child as UIElement;
    }

    private static async Task ReloadAsync(IPageViewModel page)
    {
        try
        {
            await page.OnNavigatedToAsync();
        }
        catch (Exception ex) when (App.Services.GetService(typeof(IDialogService)) is IDialogService dialogs)
        {
            dialogs.Error(ClothingStore.Core.Localization.Loc.T("Common.SomethingWentWrong"), ex);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = SearchField.FocusFirst(PageHost);
        }
        else if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            if (App.Services.GetService(typeof(IDialogService)) is IDialogService dialogs)
                dialogs.Info(ClothingStore.Core.Localization.Loc.T("Shell.ShortcutsHelp"), ClothingStore.Core.Localization.Loc.T("Shell.ShortcutsTitle"));
        }
        else if (RunPageShortcut(e))
        {
            e.Handled = true;
        }
        else if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None && PageHost.Page is IPageViewModel page)
        {
            // Reloads the page's data, as when opening it from the sidebar (the register keeps its cart).
            e.Handled = true;
            _ = ReloadAsync(page);
        }
    }
}
