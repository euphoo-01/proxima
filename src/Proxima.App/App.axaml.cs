using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Proxima.App.ViewModels;
using Proxima.Infrastructure.Assets;
using Proxima.Infrastructure.Auth;
using Proxima.Infrastructure.Goals;
using Proxima.Infrastructure.Portfolios;
using Proxima.Infrastructure.Quotes;
using Proxima.Infrastructure.Settings;
using Proxima.Infrastructure.Taxes;
using Proxima.Infrastructure.Transactions;
using Proxima.Importing;

namespace Proxima.App;

public partial class App : global::Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string profileStorePath = ProximaAuthComposition.GetDefaultProfileStorePath();
            string portfolioStorePath = ProximaPortfolioComposition.GetDefaultPortfolioStorePath();
            string assetStorePath = ProximaAssetComposition.GetDefaultAssetStorePath();
            string transactionStorePath = ProximaTransactionComposition.GetDefaultTransactionStorePath();
            string quoteCachePath = ProximaQuoteComposition.GetDefaultQuoteCacheStorePath();
            string goalsStorePath = ProximaGoalComposition.GetDefaultGoalsStorePath();
            string settingsStorePath = ProximaSettingsComposition.GetDefaultSettingsStorePath();
            ShellViewModel shell = new(
                new ShellNavigationService(),
                ProximaPortfolioComposition.CreatePortfolioService(portfolioStorePath),
                ProximaAssetComposition.CreateAssetService(assetStorePath),
                ProximaTransactionComposition.CreateTransactionService(transactionStorePath, assetStorePath),
                ProximaImportComposition.CreateImportService(),
                ProximaQuoteComposition.CreateQuoteRefreshService(assetStorePath, quoteCachePath),
                ProximaGoalComposition.CreateGoalService(goalsStorePath),
                ProximaTaxComposition.CreateTaxCalculator(),
                ProximaSettingsComposition.CreateSettingsService(settingsStorePath));
            desktop.MainWindow = new MainWindow(new AuthViewModel(ProximaAuthComposition.CreateLocalAuthService(profileStorePath), shell));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
