using Proxima.App.Navigation;
using Proxima.App.ViewModels;
using Proxima.App.ViewModels.Placeholder;
using Proxima.App.Views.Assets;
using Proxima.App.Views.AssetDetails;
using Proxima.App.Views.Dashboard;
using Proxima.App.Views.Goals;
using Proxima.App.Views.Import;
using Proxima.App.Views.Notifications;
using Proxima.App.Views.Profile;
using Proxima.App.Views.Settings;
using Proxima.App.Views.Support;
using Proxima.App.Views.Taxes;

namespace Proxima.App.Shell;

public sealed class AppShellViewModel : ViewModelBase
{
    private readonly IAppNavigationService _navigation;
    private readonly IShellState _shellState;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly AssetsViewModel _assetsViewModel;
    private readonly AssetDetailsViewModel _assetDetailsViewModel;
    private readonly ManualImportViewModel _manualImportViewModel;
    private readonly GoalsViewModel _goalsViewModel;
    private readonly TaxesViewModel _taxesViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly SupportViewModel _supportViewModel;
    private readonly NotificationsViewModel _notificationsViewModel;
    private readonly ProfileViewModel _profileViewModel;
    private object? _currentContent;

    public AppShellViewModel(
        IAppNavigationService navigation,
        SidebarViewModel sidebar,
        TopbarViewModel topbar,
        IShellState shellState,
        DashboardViewModel dashboardViewModel,
        AssetsViewModel assetsViewModel,
        AssetDetailsViewModel assetDetailsViewModel,
        ManualImportViewModel manualImportViewModel,
        GoalsViewModel goalsViewModel,
        TaxesViewModel taxesViewModel,
        SettingsViewModel settingsViewModel,
        SupportViewModel supportViewModel,
        NotificationsViewModel notificationsViewModel,
        ProfileViewModel profileViewModel)
    {
        _navigation = navigation;
        _shellState = shellState;
        _dashboardViewModel = dashboardViewModel;
        _assetsViewModel = assetsViewModel;
        _assetDetailsViewModel = assetDetailsViewModel;
        _manualImportViewModel = manualImportViewModel;
        _goalsViewModel = goalsViewModel;
        _taxesViewModel = taxesViewModel;
        _settingsViewModel = settingsViewModel;
        _supportViewModel = supportViewModel;
        _notificationsViewModel = notificationsViewModel;
        _profileViewModel = profileViewModel;

        Sidebar = sidebar;
        Topbar = topbar;

        RegisterRoutes();

        _navigation.RouteChanged += HandleRouteChanged;
        _shellState.PortfolioChanged += HandlePortfolioChanged;

        _navigation.Navigate(AppRoutes.Dashboard);
    }

    public SidebarViewModel Sidebar { get; }

    public TopbarViewModel Topbar { get; }

    public object? CurrentContent
    {
        get => _currentContent;
        private set => SetProperty(ref _currentContent, value);
    }

    private void RegisterRoutes()
    {
        _navigation.Register(new AppRoute(AppRoutes.Dashboard, "Дешборд", "Дешборд"));
        _navigation.Register(new AppRoute(AppRoutes.Assets, "Все активы", "Все активы"));
        _navigation.Register(new AppRoute(AppRoutes.ImportPreview, "Импорт", "Импорт"));
        _navigation.Register(new AppRoute(AppRoutes.ManualImport, "Импорт", "Импорт"));
        _navigation.Register(new AppRoute(AppRoutes.AssetDetails, "Детальная информация", "Все активы / Актив"));
        _navigation.Register(new AppRoute(AppRoutes.Goals, "Цели", "Цели"));
        _navigation.Register(new AppRoute(AppRoutes.Taxes, "Налоги", "Налоги"));
        _navigation.Register(new AppRoute(AppRoutes.Settings, "Настройки", "Настройки"));
        _navigation.Register(new AppRoute(AppRoutes.Notifications, "Уведомления", "Уведомления"));
        _navigation.Register(new AppRoute(AppRoutes.Support, "Поддержка", "Поддержка"));
        _navigation.Register(new AppRoute(AppRoutes.Profile, "Профиль", "Профиль"));
    }

    private void HandleRouteChanged(AppRoute route)
    {
        Topbar.Update(route.Title, route.Breadcrumb, _shellState.CurrentPortfolioName);

        CurrentContent = route.Key switch
        {
            AppRoutes.Dashboard => _dashboardViewModel,
            AppRoutes.Assets => _assetsViewModel,
            AppRoutes.ImportPreview => _manualImportViewModel,
            AppRoutes.AssetDetails => _assetDetailsViewModel,
            AppRoutes.ManualImport => _manualImportViewModel,
            AppRoutes.Goals => _goalsViewModel,
            AppRoutes.Taxes => _taxesViewModel,
            AppRoutes.Settings => _settingsViewModel,
            AppRoutes.Notifications => _notificationsViewModel,
            AppRoutes.Support => _supportViewModel,
            AppRoutes.Profile => _profileViewModel,
            _ => new PlaceholderViewModel(route.Title, "Screen is not migrated yet. PlaceholderView is shown by runtime shell.")
        };
    }

    private void HandlePortfolioChanged(object? sender, ShellPortfolioChangedEventArgs e)
    {
        AppRoute current = _navigation.Current;
        Topbar.Update(current.Title, current.Breadcrumb, e.PortfolioName);
    }
}
