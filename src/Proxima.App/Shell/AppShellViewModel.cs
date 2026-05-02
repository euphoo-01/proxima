using Proxima.App.Navigation;
using Proxima.App.ViewModels;
using Proxima.App.ViewModels.Placeholder;
using Proxima.App.Views.Assets;
using Proxima.App.Views.AssetDetails;
using Proxima.App.Views.Dashboard;
using Proxima.App.Views.Import;

namespace Proxima.App.Shell;

public sealed class AppShellViewModel : ViewModelBase
{
    private readonly IAppNavigationService _navigation;
    private readonly IShellState _shellState;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly AssetsViewModel _assetsViewModel;
    private readonly AssetDetailsViewModel _assetDetailsViewModel;
    private readonly ManualImportViewModel _manualImportViewModel;

    public AppShellViewModel(
        IAppNavigationService navigation,
        SidebarViewModel sidebar,
        TopbarViewModel topbar,
        IShellState shellState,
        DashboardViewModel dashboardViewModel,
        AssetsViewModel assetsViewModel,
        AssetDetailsViewModel assetDetailsViewModel,
        ManualImportViewModel manualImportViewModel)
    {
        _navigation = navigation;
        _shellState = shellState;
        _dashboardViewModel = dashboardViewModel;
        _assetsViewModel = assetsViewModel;
        _assetDetailsViewModel = assetDetailsViewModel;
        _manualImportViewModel = manualImportViewModel;
        Sidebar = sidebar;
        Topbar = topbar;

        RegisterRoutes();
        _navigation.RouteChanged += HandleRouteChanged;
        _navigation.Navigate(AppRoutes.Dashboard);
    }

    public SidebarViewModel Sidebar { get; }

    public TopbarViewModel Topbar { get; }

    private object? _currentContent;
    public object? CurrentContent
    {
        get => _currentContent;
        private set => SetProperty(ref _currentContent, value);
    }

    private void RegisterRoutes()
    {
        _navigation.Register(new AppRoute(AppRoutes.Dashboard, "Dashboard", "Dashboard"));
        _navigation.Register(new AppRoute(AppRoutes.Assets, "Все активы", "Все активы"));
        _navigation.Register(new AppRoute(AppRoutes.ManualImport, "Ручной импорт", "Все активы / Ручной импорт"));
        _navigation.Register(new AppRoute(AppRoutes.AssetDetails, "Asset Details", "Все активы / Asset"));
        _navigation.Register(new AppRoute(AppRoutes.Goals, "Goals", "Goals"));
        _navigation.Register(new AppRoute(AppRoutes.Taxes, "Taxes", "Taxes"));
        _navigation.Register(new AppRoute(AppRoutes.Settings, "Settings", "Settings"));
    }

    private void HandleRouteChanged(AppRoute route)
    {
        Topbar.Update(route.Title, route.Breadcrumb, _shellState.CurrentPortfolioName);
        CurrentContent = route.Key switch
        {
            AppRoutes.Dashboard => _dashboardViewModel,
            AppRoutes.Assets => _assetsViewModel,
            AppRoutes.AssetDetails => _assetDetailsViewModel,
            AppRoutes.ManualImport => _manualImportViewModel,
            _ => new PlaceholderViewModel(route.Title, "Screen is not migrated yet. PlaceholderView is shown by runtime shell.")
        };
    }
}
