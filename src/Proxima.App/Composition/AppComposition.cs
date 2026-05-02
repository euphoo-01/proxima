using Microsoft.Extensions.DependencyInjection;
using Proxima.App.Navigation;
using Proxima.App.Shell;
using Proxima.App.ViewModels;
using Proxima.App.Views.Dashboard;
using Proxima.Infrastructure.Assets;
using Proxima.Infrastructure.Auth;
using Proxima.Infrastructure.Goals;
using Proxima.Infrastructure.Portfolios;
using Proxima.Infrastructure.Quotes;
using Proxima.Infrastructure.Settings;
using Proxima.Infrastructure.Taxes;
using Proxima.Infrastructure.Transactions;
using Proxima.Importing;
using Proxima.Reporting.Reports;
using Proxima.Sync.Snapshots;

namespace Proxima.App.Composition;

public static class AppComposition
{
    public static ServiceProvider BuildServiceProvider()
    {
        ServiceCollection services = new();

        string profileStorePath = ProximaAuthComposition.GetDefaultProfileStorePath();
        string portfolioStorePath = ProximaPortfolioComposition.GetDefaultPortfolioStorePath();
        string assetStorePath = ProximaAssetComposition.GetDefaultAssetStorePath();
        string transactionStorePath = ProximaTransactionComposition.GetDefaultTransactionStorePath();
        string quoteCachePath = ProximaQuoteComposition.GetDefaultQuoteCacheStorePath();
        string goalsStorePath = ProximaGoalComposition.GetDefaultGoalsStorePath();
        string settingsStorePath = ProximaSettingsComposition.GetDefaultSettingsStorePath();

        services.AddSingleton(new ShellViewModel(
            new ShellNavigationService(),
            ProximaPortfolioComposition.CreatePortfolioService(portfolioStorePath),
            ProximaAssetComposition.CreateAssetService(assetStorePath),
            ProximaTransactionComposition.CreateTransactionService(transactionStorePath, assetStorePath),
            ProximaImportComposition.CreateImportService(),
            ProximaQuoteComposition.CreateQuoteRefreshService(assetStorePath, quoteCachePath, settingsStorePath),
            ProximaGoalComposition.CreateGoalService(goalsStorePath),
            ProximaTaxComposition.CreateTaxCalculator(settingsStorePath),
            ProximaSettingsComposition.CreateSettingsService(settingsStorePath),
            ProximaSyncComposition.CreateSnapshotService(),
            ProximaReportingComposition.CreateReportService()));

        services.AddSingleton(_ => ProximaAuthComposition.CreateLocalAuthService(profileStorePath));
        services.AddSingleton<AuthViewModel>();

        services.AddSingleton<IAppNavigationService, AppNavigationService>();
        services.AddSingleton<IShellState, MockShellState>();
        services.AddSingleton<IDashboardDataProvider, MockDashboardDataProvider>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<SidebarViewModel>();
        services.AddSingleton<TopbarViewModel>();
        services.AddSingleton<AppShellViewModel>();
        services.AddTransient<AppShellView>();

        return services.BuildServiceProvider();
    }
}
