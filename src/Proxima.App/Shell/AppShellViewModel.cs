using Proxima.App.ViewModels;

namespace Proxima.App.Shell;

public sealed class AppShellViewModel : ViewModelBase
{
    public AppShellViewModel()
    {
        Sidebar = new SidebarViewModel(OnRouteChanged);
        Topbar = new TopbarViewModel();
        _currentRouteLabel = Sidebar.ActiveRouteLabel;
        Topbar.SetCurrentSection(_currentRouteLabel);
    }

    public SidebarViewModel Sidebar { get; }

    public TopbarViewModel Topbar { get; }

    private string _currentRouteLabel;
    public string CurrentRouteLabel
    {
        get => _currentRouteLabel;
        private set => SetProperty(ref _currentRouteLabel, value);
    }

    private void OnRouteChanged(string routeLabel)
    {
        CurrentRouteLabel = routeLabel;
        Topbar.SetCurrentSection(routeLabel);
    }
}
