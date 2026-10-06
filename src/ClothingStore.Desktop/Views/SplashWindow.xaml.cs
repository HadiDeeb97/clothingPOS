using System.Windows;
using ClothingStore.Desktop.Infrastructure;

namespace ClothingStore.Desktop.Views;

/// <summary>Shown straight away while the app connects to the database, so starting never looks frozen.</summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        WindowAppearance.Apply(this);
    }
}
