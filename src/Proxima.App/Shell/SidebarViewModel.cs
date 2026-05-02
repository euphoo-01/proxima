using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.Navigation;
using Proxima.App.ViewModels;

namespace Proxima.App.Shell;

public sealed class SidebarViewModel : ViewModelBase
{
    private readonly IAppNavigationService _navigation;

    public SidebarViewModel(IAppNavigationService navigation)
    {
        _navigation = navigation;
        Items = new ObservableCollection<SidebarItemViewModel>
        {
            new(AppRoutes.Dashboard, "Дешборд"),
            new(AppRoutes.Assets, "Assets"),
            new(AppRoutes.AssetDetails, "AssetDetails"),
            new(AppRoutes.Goals, "Goals"),
            new(AppRoutes.Taxes, "Taxes"),
            new(AppRoutes.Settings, "Settings")
        };

        NavigateCommand = new DelegateCommand(ExecuteNavigate);
        _navigation.RouteChanged += OnRouteChanged;
    }

    public ObservableCollection<SidebarItemViewModel> Items { get; }

    public ICommand NavigateCommand { get; }

    private void ExecuteNavigate(object? parameter)
    {
        if (parameter is SidebarItemViewModel item)
        {
            _navigation.Navigate(item.RouteKey);
        }
    }

    private void OnRouteChanged(AppRoute route)
    {
        foreach (SidebarItemViewModel item in Items)
        {
            item.IsActive = string.Equals(item.RouteKey, route.Key, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        private readonly Action<object?> _execute = execute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class SidebarItemViewModel(string routeKey, string label) : ViewModelBase
{
    public string RouteKey { get; } = routeKey;

    public string Label { get; } = label;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
