using Microsoft.Extensions.DependencyInjection;
using Proxima.App.Navigation;
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
using Proxima.Core.Application.AssetDetails;
using Proxima.Core.Application.Auth;
using Proxima.Core.Application.Goals;
using Proxima.Core.Application.Importing;
using Proxima.Core.Application.Portfolios;
using Proxima.Core.Application.Taxes;
using Proxima.Core.Application.Transactions;
using Proxima.Infrastructure.AssetDetails;
using Proxima.Infrastructure.Auth;
using Proxima.Infrastructure.Assets;
using Proxima.Infrastructure.Goals;
using Proxima.Infrastructure.Notifications;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Portfolios;
using Proxima.Infrastructure.Quotes;
using Proxima.Infrastructure.MarketData;
using Proxima.Infrastructure.Settings;
using Proxima.Infrastructure.Taxes;
using Proxima.Infrastructure.Importing;
using Proxima.Core.Application.Reporting;
using Proxima.Infrastructure.Reporting;
using Proxima.Infrastructure.Transactions;

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

        services.AddSingleton<IAuthGateService, LocalProfileAuthGateService>();
        services.AddSingleton<IRuntimeAuthBootstrapper, RuntimeAuthBootstrapper>();

        services.AddSingleton(_ => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12),
        });

        services
            .AddPersistenceModule(databaseOptions, db)
            .AddAuthModule()
            .AddPortfolioModule()
            .AddAssetModule()
            .AddTransactionModule()
            .AddGoalModule()
            .AddSettingsModule()
            .AddQuoteModule()
            .AddTaxModule()
            .AddImportModule()
            .AddReportingModule()
            .AddAuditModule()
            .AddNotificationModule();

        services.AddSingleton<IAppNotificationCenter, AppNotificationCenter>();

        services.AddSingleton<RuntimeShellState>();
        services.AddSingleton<IShellState>(provider => provider.GetRequiredService<RuntimeShellState>());
        services.AddSingleton<IShellPortfolioCoordinator>(provider => provider.GetRequiredService<RuntimeShellState>());
        services.AddSingleton<ICurrentPortfolioContext>(provider => provider.GetRequiredService<RuntimeShellState>());

        services.AddSingleton<IRuntimeDataInvalidation, RuntimeDataInvalidation>();

        services.AddSingleton<IImportPreviewGateway>(provider =>
            new ImportPreviewGateway(provider.GetRequiredService<IImportService>()));

        services.AddSingleton<IDashboardDataProvider, RuntimeDashboardDataProvider>();

        services.AddSingleton<TwelveDataAssetMarketDataProvider>();
        services.AddSingleton<IAssetDetailsService, AssetDetailsService>();

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
        services.AddSingleton<IGoalProjectionService, GoalProjectionService>();
        services.AddSingleton<IHistoricalPortfolioReturnService, HistoricalPortfolioReturnService>();
        services.AddSingleton<IGoalProgressBaselineService, GoalProgressBaselineService>();

        services.AddSingleton<SidebarViewModel>();
        services.AddSingleton<TopbarViewModel>();
        services.AddSingleton<AppShellViewModel>();
        services.AddTransient<AppShellView>();

        return services.BuildServiceProvider();
    }
}
