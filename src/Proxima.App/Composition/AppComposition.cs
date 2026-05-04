using Microsoft.Extensions.DependencyInjection;
using Proxima.App.Navigation;
using Proxima.App.Shell;
using Proxima.App.Views.Assets;
using Proxima.App.Views.AssetDetails;
using Proxima.App.Views.Dashboard;
using Proxima.App.Views.Goals;
using Proxima.App.Views.Import;
using Proxima.App.Views.Auth;
using Proxima.App.Views.Settings;
using Proxima.App.Views.Taxes;
using Proxima.Application.Assets;
using Proxima.Application.Auth;
using Proxima.Application.Goals;
using Proxima.Application.Observability;
using Proxima.Application.Portfolios;
using Proxima.Application.Quotes;
using Proxima.Application.Settings;
using Proxima.Application.Taxes;
using Proxima.Application.Transactions;
using Proxima.Infrastructure.Auth;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Persistence.Repositories;
using Proxima.Infrastructure.Quotes;
using Proxima.Infrastructure.Settings;
using Proxima.Infrastructure.Taxes;
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
        DatabaseOptions databaseOptions = DatabaseConnectionStringProvider.Resolve();
        DatabaseBootstrapService db = new(databaseOptions);

        services.AddSingleton<IAppNavigationService, AppNavigationService>();
        services.AddSingleton<IRuntimeUserContext, RuntimeUserContext>();
        services.AddSingleton<ILocalAuthService>(_ => ProximaAuthComposition.CreateLocalAuthService(profileStorePath));
        services.AddSingleton<IAuthGateService, LocalProfileAuthGateService>();
        services.AddSingleton<IRuntimeAuthBootstrapper, RuntimeAuthBootstrapper>();
        services.AddSingleton(databaseOptions);
        services.AddSingleton(db);
        services.AddSingleton<IProximaUnitOfWorkAccessor, ProximaUnitOfWorkAccessor>();
        services.AddSingleton<IProximaUnitOfWorkFactory, ProximaUnitOfWorkFactory>();
        services.AddSingleton<IPortfolioRepository, PostgresPortfolioRepository>();
        services.AddSingleton<IAssetRepository, PostgresAssetRepository>();
        services.AddSingleton<ITransactionRepository, PostgresTransactionRepository>();
        services.AddSingleton<IGoalRepository, PostgresGoalRepository>();
        services.AddSingleton<IUserSettingsRepository, PostgresUserSettingsRepository>();
        services.AddSingleton<IQuoteCacheRepository, PostgresQuoteCacheRepository>();
        services.AddSingleton<IAuditLogRepository, PostgresAuditLogRepository>();
        services.AddSingleton<IPortfolioService, PortfolioService>();
        services.AddSingleton<IAssetService, AssetService>();
        services.AddSingleton<ITransactionService, TransactionService>();
        services.AddSingleton<IImportCommitService, PostgresImportCommitService>();
        services.AddSingleton<IGoalService, GoalService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton(_ => new HttpClient());
        services.AddSingleton(_ => new LocalSettingsReader(ProximaSettingsComposition.GetDefaultSettingsStorePath()));
        services.AddSingleton<IQuoteProvider, ConfigurableQuoteProvider>();
        services.AddSingleton<IQuoteRefreshService, QuoteRefreshService>();
        services.AddSingleton<IShellState, MockShellState>();
        services.AddSingleton<IImportPreviewGateway>(provider => new ImportPreviewGateway(ProximaImportComposition.CreateImportService()));
        services.AddSingleton<IDashboardDataProvider, MockDashboardDataProvider>();
        services.AddSingleton<IAssetDetailsReadModelProvider, MockAssetDetailsReadModelProvider>();
        services.AddSingleton<ITaxCalculator>(_ => ProximaTaxComposition.CreateTaxCalculator(Proxima.Infrastructure.Settings.ProximaSettingsComposition.GetDefaultSettingsStorePath()));
        services.AddSingleton<IReportService>(_ => ProximaReportingComposition.CreateReportService());
        services.AddSingleton<IGoalProjectionService, GoalProjectionService>();
        services.AddSingleton<DashboardViewModel>();
        services.AddTransient<LoginViewModel>();
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
