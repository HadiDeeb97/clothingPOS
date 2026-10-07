using System.Windows;
using System.Windows.Controls;

namespace ClothingStore.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>Section list on the left: scrolls that section to the top.</summary>
    private void GoTo(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: FrameworkElement section }) return;
        var top = section.TransformToAncestor(Sections).Transform(new Point(0, 0)).Y;
        Scroller.ScrollToVerticalOffset(top);
    }
}
