using Avalonia.Controls;
using Avalonia.Input;
using Proxima.App.ViewModels;
using Proxima.Infrastructure.Assets;
using Proxima.Infrastructure.Auth;
using Proxima.Infrastructure.Goals;
using Proxima.Infrastructure.Portfolios;
using Proxima.Infrastructure.Quotes;
using Proxima.Infrastructure.Transactions;
using Proxima.Importing;

namespace Proxima.App;

public partial class MainWindow : Window
{
    public MainWindow()
        : this(CreateDefaultViewModel())
    {
    }

    public MainWindow(AuthViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Opened += async (_, _) => await viewModel.InitializeAsync().ConfigureAwait(true);
    }

    private AuthViewModel ViewModel => (AuthViewModel)DataContext!;

    private static AuthViewModel CreateDefaultViewModel()
    {
        string profileStorePath = ProximaAuthComposition.GetDefaultProfileStorePath();
        string portfolioStorePath = ProximaPortfolioComposition.GetDefaultPortfolioStorePath();
        string assetStorePath = ProximaAssetComposition.GetDefaultAssetStorePath();
        string transactionStorePath = ProximaTransactionComposition.GetDefaultTransactionStorePath();
        string quoteCachePath = ProximaQuoteComposition.GetDefaultQuoteCacheStorePath();
        string goalsStorePath = ProximaGoalComposition.GetDefaultGoalsStorePath();
        ShellViewModel shell = new(
            new ShellNavigationService(),
            ProximaPortfolioComposition.CreatePortfolioService(portfolioStorePath),
            ProximaAssetComposition.CreateAssetService(assetStorePath),
            ProximaTransactionComposition.CreateTransactionService(transactionStorePath, assetStorePath),
            ProximaImportComposition.CreateImportService(),
            ProximaQuoteComposition.CreateQuoteRefreshService(assetStorePath, quoteCachePath),
            ProximaGoalComposition.CreateGoalService(goalsStorePath));
        return new AuthViewModel(ProximaAuthComposition.CreateLocalAuthService(profileStorePath), shell);
    }

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

    private async void SaveCreatePortfolioClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.CreatePortfolioAsync().ConfigureAwait(true);
    }

    private void OpenManagePortfolioClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.OpenManagePortfolioDialog();
    }

    private void CancelManagePortfolioClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.CancelManagePortfolioDialog();
    }

    private async void SaveManagePortfolioClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.SavePortfolioChangesAsync().ConfigureAwait(true);
    }

    private async void ArchivePortfolioClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.ArchiveSelectedPortfolioAsync().ConfigureAwait(true);
    }

    private void OpenCreateAssetClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.OpenCreateAssetDialog();
    }

    private void CancelCreateAssetClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.CancelCreateAssetDialog();
    }

    private async void SaveCreateAssetClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.CreateAssetAsync().ConfigureAwait(true);
    }

    private void OpenEditAssetClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: AssetRowViewModel asset })
        {
            ViewModel.Shell.OpenEditAssetDialog(asset);
        }
    }

    private void CancelEditAssetClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.CancelEditAssetDialog();
    }

    private async void SaveEditAssetClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.SaveAssetChangesAsync().ConfigureAwait(true);
    }

    private async void ArchiveAssetClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.ArchiveSelectedAssetAsync().ConfigureAwait(true);
    }

    private void SelectAssetClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: AssetRowViewModel asset })
        {
            ViewModel.Shell.SelectedAsset = asset;
            ViewModel.Shell.OpenAssetDetails();
        }
    }

    private void OpenCreateTransactionClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.OpenCreateTransactionDialog();
    }

    private void OpenCreateTransactionForAssetClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: AssetRowViewModel asset })
        {
            ViewModel.Shell.StartCreateTransactionForAsset(asset);
        }
    }

    private void CancelCreateTransactionClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.CancelCreateTransactionDialog();
    }

    private async void SaveCreateTransactionClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.CreateTransactionAsync().ConfigureAwait(true);
    }

    private void OpenEditTransactionClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TransactionRowViewModel row })
        {
            ViewModel.Shell.OpenEditTransactionDialog(row);
        }
    }

    private void CancelEditTransactionClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.CancelEditTransactionDialog();
    }

    private async void SaveEditTransactionClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.SaveTransactionChangesAsync().ConfigureAwait(true);
    }

    private async void ArchiveTransactionClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.ArchiveSelectedTransactionAsync().ConfigureAwait(true);
    }

    private void OpenImportAssetsClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.OpenImportDialog();
    }

    private void CancelImportDialogClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.CancelImportDialog();
    }

    private async void PreviewImportClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.PreviewImportAsync().ConfigureAwait(true);
    }

    private async void CommitImportClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.CommitImportAsync().ConfigureAwait(true);
    }

    private async void RefreshQuotesClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.RefreshQuotesAsync().ConfigureAwait(true);
    }

    private void OpenCreateGoalClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.OpenCreateGoalDialog();
    }

    private void CancelCreateGoalClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.CancelCreateGoalDialog();
    }

    private async void SaveCreateGoalClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.CreateGoalAsync().ConfigureAwait(true);
    }

    private void OpenEditGoalClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: GoalRowViewModel goal })
        {
            ViewModel.Shell.OpenEditGoalDialog(goal);
        }
    }

    private void CancelEditGoalClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.Shell.CancelEditGoalDialog();
    }

    private async void SaveEditGoalClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.SaveGoalChangesAsync().ConfigureAwait(true);
    }

    private async void ArchiveGoalClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.Shell.ArchiveSelectedGoalAsync().ConfigureAwait(true);
    }
}
