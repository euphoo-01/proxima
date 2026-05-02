using Proxima.App.Navigation;
using Proxima.App.ViewModels;
using Proxima.App.ViewModels.Placeholder;

namespace Proxima.App.Shell;

public sealed class AppShellViewModel : ViewModelBase
{
    private readonly IAppNavigationService _navigation;
    private readonly IShellState _shellState;

    public AppShellViewModel(IAppNavigationService navigation, SidebarViewModel sidebar, TopbarViewModel topbar, IShellState shellState)
    {
        _navigation = navigation;
        _shellState = shellState;
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
        _navigation.Register(new AppRoute(AppRoutes.Assets, "Assets", "Assets"));
        _navigation.Register(new AppRoute(AppRoutes.AssetDetails, "Asset Details", "Assets / Asset Details"));
        _navigation.Register(new AppRoute(AppRoutes.Goals, "Goals", "Goals"));
        _navigation.Register(new AppRoute(AppRoutes.Taxes, "Taxes", "Taxes"));
        _navigation.Register(new AppRoute(AppRoutes.Settings, "Settings", "Settings"));
    }

    private void HandleRouteChanged(AppRoute route)
    {
        Topbar.Update(route.Title, route.Breadcrumb, _shellState.CurrentPortfolioName);
        CurrentContent = new PlaceholderViewModel(route.Title, "Screen is not migrated yet. PlaceholderView is shown by runtime shell.");
    }
}
