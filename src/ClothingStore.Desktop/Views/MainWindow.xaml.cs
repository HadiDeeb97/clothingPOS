using System.Windows;
using ClothingStore.Desktop.Infrastructure;

namespace ClothingStore.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WindowAppearance.Apply(this);
    }
}
