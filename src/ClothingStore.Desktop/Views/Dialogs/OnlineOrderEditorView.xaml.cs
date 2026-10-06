using System.Windows.Controls;

namespace ClothingStore.Desktop.Views.Dialogs;

public partial class OnlineOrderEditorView : UserControl
{
    public OnlineOrderEditorView()
    {
        InitializeComponent();
        Loaded += (_, _) => AddBox.Focus();
    }
}
