using System.Windows;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.ViewModels;

namespace ClothingStore.Desktop.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        WindowAppearance.Apply(this);
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.SignedIn += (_, _) => DialogResult = true;
        viewModel.LanguageRequested += (_, _) => DialogResult = false;
        Loaded += async (_, _) =>
        {
            UsernameBox.Focus();
            await viewModel.InitializeAsync();
        };
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.Password = PasswordBox.Password;
}
