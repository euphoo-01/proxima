using Microsoft.Extensions.DependencyInjection;
using Proxima.App.Navigation;
using Proxima.App.Shell;
using Proxima.App.Views.Assets;
using Proxima.App.Views.AssetDetails;
using Proxima.App.Views.Dashboard;
using Proxima.App.Views.Goals;
using Proxima.App.Views.Import;
using Proxima.App.Views.Settings;
using Proxima.App.Views.Taxes;
using Proxima.Application.Taxes;
using Proxima.Application.Transactions;
using Proxima.Infrastructure.Assets;
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

        string portfolioStorePath = ProximaPortfolioComposition.GetDefaultPortfolioStorePath();
        string assetStorePath = ProximaAssetComposition.GetDefaultAssetStorePath();
        string transactionStorePath = ProximaTransactionComposition.GetDefaultTransactionStorePath();
        string quoteCachePath = ProximaQuoteComposition.GetDefaultQuoteCacheStorePath();
        string goalsStorePath = ProximaGoalComposition.GetDefaultGoalsStorePath();
        string settingsStorePath = ProximaSettingsComposition.GetDefaultSettingsStorePath();

        services.AddSingleton<IAppNavigationService, AppNavigationService>();
        services.AddSingleton<IShellState, MockShellState>();
        services.AddSingleton<IImportPreviewGateway>(provider => new ImportPreviewGateway(ProximaImportComposition.CreateImportService()));
        services.AddSingleton<IDashboardDataProvider, MockDashboardDataProvider>();
        services.AddSingleton<IAssetDetailsReadModelProvider, MockAssetDetailsReadModelProvider>();
        services.AddSingleton<ITransactionService>(_ => ProximaTransactionComposition.CreateTransactionService(transactionStorePath, assetStorePath));
        services.AddSingleton<ITaxCalculator>(_ => ProximaTaxComposition.CreateTaxCalculator(settingsStorePath));
        services.AddSingleton<IReportService>(_ => ProximaReportingComposition.CreateReportService());
        services.AddSingleton(_ => ProximaGoalComposition.CreateGoalService(goalsStorePath));
        services.AddSingleton<IGoalProjectionService, GoalProjectionService>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ManualImportViewModel>();
        services.AddSingleton<ImportDialogViewModel>();
        services.AddSingleton<AssetsViewModel>();
        services.AddSingleton<AssetDetailsViewModel>();
        services.AddSingleton<GoalsViewModel>();
        services.AddSingleton<TaxesViewModel.ITaxesReadModelProvider>(provider =>
            new TaxesViewModel.AppTaxesReadModelProvider(
                provider.GetRequiredService<ITransactionService>(),
                provider.GetRequiredService<ITaxCalculator>(),
                provider.GetRequiredService<IReportService>(),
                provider.GetRequiredService<IShellState>()));
        services.AddSingleton<TaxesViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SidebarViewModel>();
        services.AddSingleton<TopbarViewModel>();
        services.AddSingleton<AppShellViewModel>();
        services.AddTransient<AppShellView>();

        return services.BuildServiceProvider();
    }
}
