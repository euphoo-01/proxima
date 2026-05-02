using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.ViewModels;

namespace Proxima.App.Shell;

public sealed class SidebarViewModel : ViewModelBase
{
    private readonly Action<string> _onRouteChanged;

    public SidebarViewModel(Action<string> onRouteChanged)
    {
        _onRouteChanged = onRouteChanged;
        Items = new ObservableCollection<SidebarItemViewModel>
        {
            new("Dashboard", "Панель"),
            new("Assets", "Активы"),
            new("Goals", "Цели"),
            new("Settings", "Настройки")
        };

        NavigateCommand = new DelegateCommand(ExecuteNavigate);
        SetActive(Items[0]);
    }

    public ObservableCollection<SidebarItemViewModel> Items { get; }

    public ICommand NavigateCommand { get; }

    private SidebarItemViewModel? _activeItem;

    public string ActiveRouteLabel => _activeItem?.Label ?? string.Empty;

    private void ExecuteNavigate(object? parameter)
    {
        if (parameter is SidebarItemViewModel item)
        {
            SetActive(item);
        }
    }

    private void SetActive(SidebarItemViewModel item)
    {
        if (_activeItem == item)
        {
            return;
        }

        if (_activeItem is not null)
        {
            _activeItem.IsActive = false;
        }

        _activeItem = item;
        _activeItem.IsActive = true;
        OnPropertyChanged(nameof(ActiveRouteLabel));
        _onRouteChanged(_activeItem.Label);
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
