using Avalonia.Controls;
using Avalonia.Input;
using Proxima.App.ViewModels;
using Proxima.Infrastructure.Auth;

namespace Proxima.App;

public partial class MainWindow : Window
{
    public MainWindow()
        : this(new AuthViewModel(ProximaAuthComposition.CreateLocalAuthService(
            ProximaAuthComposition.GetDefaultProfileStorePath())))
    {
    }

    public MainWindow(AuthViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Opened += async (_, _) => await viewModel.InitializeAsync().ConfigureAwait(true);
    }

    private AuthViewModel ViewModel => (AuthViewModel)DataContext!;

    private TextBox SetupPassword => this.FindControl<TextBox>("SetupPasswordBox")!;

    private TextBox SetupPasswordConfirmation => this.FindControl<TextBox>("SetupPasswordConfirmationBox")!;

    private TextBox UnlockPassword => this.FindControl<TextBox>("UnlockPasswordBox")!;

    private void SetupPasswordChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.PreviewSetupPassword(SetupPassword.Text ?? string.Empty, SetupPasswordConfirmation.Text ?? string.Empty);
    }

    private async void CreateProfileClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.CreateProfileAsync(
            SetupPassword.Text ?? string.Empty,
            SetupPasswordConfirmation.Text ?? string.Empty).ConfigureAwait(true);

        SetupPassword.Text = string.Empty;
        SetupPasswordConfirmation.Text = string.Empty;
    }

    private async void UnlockClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.UnlockAsync(UnlockPassword.Text ?? string.Empty).ConfigureAwait(true);
        UnlockPassword.Text = string.Empty;
    }

    private async void UnlockPasswordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await ViewModel.UnlockAsync(UnlockPassword.Text ?? string.Empty).ConfigureAwait(true);
            UnlockPassword.Text = string.Empty;
        }
    }

    private void ForgotPasswordClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.ToggleRecoveryInfo();
    }

    private void NavigateSidebarClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: string route })
        {
            ViewModel.Shell.Navigate(route);
        }
    }

    private void GoBackClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.GoBack();
    }

    private void OpenAssetDetailsClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.OpenAssetDetails();
    }

    private void OpenManualImportClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.OpenManualImport();
    }

    private void OpenCreatePortfolioClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.OpenCreatePortfolioDialog();
    }

    private void CancelCreatePortfolioClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.CancelCreatePortfolioDialog();
    }

    private void SaveCreatePortfolioClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.CreatePortfolio();
    }
}
