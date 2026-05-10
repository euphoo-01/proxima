using Microsoft.Extensions.DependencyInjection;
using Proxima.App.Navigation;
using Proxima.Infrastructure.Notifications;
using Proxima.Application.Notifications;
using Proxima.App.Notifications;
using Proxima.App.Shell;
using Proxima.App.Views.Assets;
using Proxima.App.Views.AssetDetails;
using Proxima.App.Views.Auth;
using Proxima.App.Views.Dashboard;
using Proxima.App.Views.Goals;
using Proxima.App.Views.Import;
using Proxima.App.Views.Notifications;
using Proxima.App.Views.Profile;
using Proxima.App.Views.Settings;
using Proxima.App.Views.Support;
using Proxima.App.Views.Taxes;
using Proxima.Application.AssetDetails;
using Proxima.Application.Assets;
using Proxima.Application.Auth;
using Proxima.Application.Goals;
using Proxima.Application.MarketData;
using Proxima.Application.Observability;
using Proxima.Application.Portfolios;
using Proxima.Application.Quotes;
using Proxima.Application.Settings;
using Proxima.Application.Taxes;
using Proxima.Application.Transactions;
using Proxima.Infrastructure.AssetDetails;
using Proxima.Infrastructure.Auth;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Persistence.Repositories;
using Proxima.Infrastructure.Quotes;
using Proxima.Infrastructure.MarketData;
using Proxima.Infrastructure.Taxes;
using Proxima.Importing;
using Proxima.Reporting.Reports;

namespace Proxima.App.Composition;

public static class AppComposition
{
    public static ServiceProvider BuildServiceProvider()
    {
        ServiceCollection services = new();

        DatabaseOptions databaseOptions = DatabaseConnectionStringProvider.Resolve();
        DatabaseBootstrapService db = new(databaseOptions);

        services.AddSingleton<IAppNavigationService, AppNavigationService>();

        services.AddSingleton<RuntimeUserContext>();
        services.AddSingleton<IRuntimeUserContext>(provider => provider.GetRequiredService<RuntimeUserContext>());
        services.AddSingleton<ICurrentUserContext>(provider => provider.GetRequiredService<RuntimeUserContext>());

        services.AddSingleton<ILocalUserRepository, PostgresLocalUserRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<PasswordPolicyValidator>();
        services.AddSingleton<ILocalAuthService, LocalAuthService>();

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
        services.AddSingleton<IAccountDeletionService, PostgresAccountDeletionService>();
        services.AddSingleton<IAuditLogRepository, PostgresAuditLogRepository>();
        services.AddSingleton<INotificationRepository, PostgresNotificationRepository>();

        services.AddSingleton<IPortfolioService, PortfolioService>();
        services.AddSingleton<IAssetService, AssetService>();
        services.AddSingleton<ITransactionService, TransactionService>();
        services.AddSingleton<IImportCommitService, PostgresImportCommitService>();
        services.AddSingleton<IGoalService, GoalService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IAppNotificationCenter, AppNotificationCenter>();

        services.AddSingleton(_ => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12),
        });

        services.AddSingleton<IQuoteProvider, ConfigurableQuoteProvider>();
        services.AddSingleton<IMarketSymbolSearchService, TwelveDataSymbolSearchService>();
        services.AddSingleton<IQuoteRefreshService, QuoteRefreshService>();

        services.AddSingleton<RuntimeShellState>();
        services.AddSingleton<IShellState>(provider => provider.GetRequiredService<RuntimeShellState>());
        services.AddSingleton<IShellPortfolioCoordinator>(provider => provider.GetRequiredService<RuntimeShellState>());
        services.AddSingleton<ICurrentPortfolioContext>(provider => provider.GetRequiredService<RuntimeShellState>());

        services.AddSingleton<IRuntimeDataInvalidation, RuntimeDataInvalidation>();

        services.AddSingleton<IImportPreviewGateway>(provider =>
            new ImportPreviewGateway(ProximaImportComposition.CreateImportService()));

        services.AddSingleton<IDashboardDataProvider, RuntimeDashboardDataProvider>();

        services.AddSingleton<TwelveDataAssetMarketDataProvider>();
        services.AddSingleton<IAssetDetailsService, AssetDetailsService>();

        services.AddSingleton<IExchangeRateProvider, ConfigurableExchangeRateProvider>();
        services.AddSingleton<ITaxCalculator, DraftTaxCalculator>();

        services.AddSingleton<IReportService>(_ => ProximaReportingComposition.CreateReportService());
        services.AddSingleton<IGoalProjectionService, GoalProjectionService>();
        services.AddSingleton<IHistoricalPortfolioReturnService, HistoricalPortfolioReturnService>();
        services.AddSingleton<IGoalProgressBaselineService, GoalProgressBaselineService>();

        services.AddSingleton<DashboardViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
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
                provider.GetRequiredService<IShellState>(),
                provider.GetRequiredService<IRuntimeUserContext>()));

        services.AddSingleton<TaxesViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SupportViewModel>();
        services.AddSingleton<NotificationsViewModel>();
        services.AddSingleton<ProfileViewModel>();

        services.AddSingleton<SidebarViewModel>();
        services.AddSingleton<TopbarViewModel>();
        services.AddSingleton<AppShellViewModel>();
        services.AddTransient<AppShellView>();

        return services.BuildServiceProvider();
    }
}
