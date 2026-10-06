using System.Windows.Controls;

namespace ClothingStore.Desktop.Views.Dialogs;

public partial class OnlineSaleView : UserControl
{
    public OnlineSaleView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            FeeBox.Focus();
            FeeBox.SelectAll();
        };
    }
}
