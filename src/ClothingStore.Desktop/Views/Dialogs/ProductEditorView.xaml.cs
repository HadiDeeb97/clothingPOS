using System.Windows.Controls;

namespace ClothingStore.Desktop.Views.Dialogs;

public partial class ProductEditorView : UserControl
{
    public ProductEditorView()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }
}
