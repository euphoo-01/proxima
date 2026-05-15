using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Proxima.App.Navigation;
using Proxima.App.ViewModels;
using Proxima.App.Views.Auth;
using Proxima.Core.Domain.Auth;

namespace Proxima.App.Shell;

public sealed class SidebarViewModel : ViewModelBase
{
    private readonly IAppNavigationService _navigation;
    private readonly IRuntimeUserContext _userContext;
    private Bitmap? _avatarBitmap;

    public SidebarViewModel(IAppNavigationService navigation, IRuntimeUserContext userContext)
    {
        _navigation = navigation;
        _userContext = userContext;

        Items =
        [
            new SidebarItemViewModel(
                AppRoutes.Dashboard,
                "Дешборд",
                "M3,3 H8 V8 H3 Z M12,3 H17 V8 H12 Z M3,12 H8 V17 H3 Z M12,12 H17 V17 H12 Z"),
            new SidebarItemViewModel(
                AppRoutes.Assets,
                "Все активы",
                "M4,5 H16 V17 H4 Z M7,8 H17 V14 M8,11 H12"),
            new SidebarItemViewModel(
                AppRoutes.Taxes,
                "Налоги",
                "M5,3 H15 V17 H5 Z M8,6 H12 M8,9 H12 M8,12 H10 M14,4 H17 V15"),
            new SidebarItemViewModel(
                AppRoutes.Goals,
                "Цели",
                "M10,18 A8,8 0 1,1 18,10 M10,14 A4,4 0 1,1 14,10 M10,10 L18,18 M15,18 H18 V15")
        ];

        NavigateCommand = new DelegateCommand(ExecuteNavigate);
        SupportCommand = new DelegateCommand(_ => _navigation.Navigate(AppRoutes.Support));
        ProfileCommand = new DelegateCommand(_ => _navigation.Navigate(AppRoutes.Profile));

        _navigation.RouteChanged += OnRouteChanged;

        LoadAvatarFromDisk();

        _userContext.ProfileChanged += (_, _) =>
        {
            LoadAvatarFromDisk();
            OnPropertyChanged(nameof(UserDisplayName));
            OnPropertyChanged(nameof(UserInitial));
            OnPropertyChanged(nameof(UserRoleDisplayName));
        };
    }

    public ObservableCollection<SidebarItemViewModel> Items { get; }

    public ICommand NavigateCommand { get; }

    public ICommand SupportCommand { get; }

    public ICommand ProfileCommand { get; }

    public string UserDisplayName => string.IsNullOrWhiteSpace(_userContext.DisplayName)
        ? "Пользователь"
        : _userContext.DisplayName;

    public string UserInitial => UserDisplayName.Trim()[..1].ToUpperInvariant();

    public string UserRoleDisplayName => _userContext.Role.ToDisplayName();

    public Bitmap? AvatarBitmap
    {
        get => _avatarBitmap;
        private set
        {
            if (ReferenceEquals(_avatarBitmap, value))
            {
                return;
            }

            Bitmap? old = _avatarBitmap;
            _avatarBitmap = value;
            old?.Dispose();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasAvatar));
            OnPropertyChanged(nameof(ShowInitialAvatar));
        }
    }

    public bool HasAvatar => AvatarBitmap is not null;

    public bool ShowInitialAvatar => !HasAvatar;

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
            item.IsActive = string.Equals(item.RouteKey, route.Key, StringComparison.OrdinalIgnoreCase)
                            || (string.Equals(item.RouteKey, AppRoutes.Assets, StringComparison.OrdinalIgnoreCase)
                                && (string.Equals(route.Key, AppRoutes.ImportPreview,
                                        StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(route.Key, AppRoutes.AssetDetails,
                                        StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(route.Key, AppRoutes.ManualImport,
                                        StringComparison.OrdinalIgnoreCase)));
        }
    }

    private void LoadAvatarFromDisk()
    {
        AvatarBitmap = null;

        if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty)
        {
            return;
        }

        string path = GetAvatarPath(_userContext.UserId);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            using MemoryStream stream = new(bytes);
            AvatarBitmap = new Bitmap(stream);
        }
        catch
        {
            AvatarBitmap = null;
        }
    }

    private static string GetProfileExtrasDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Proxima",
            "Profile");
    }

    private static string GetAvatarPath(Guid userId)
    {
        return Path.Combine(GetProfileExtrasDirectory(), $"{userId:N}.avatar");
    }

    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        private readonly Action<object?> _execute = execute;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);
    }
}

public sealed class SidebarItemViewModel : ViewModelBase
{
    private bool _isActive;

    public SidebarItemViewModel(string routeKey, string label, string iconData)
    {
        RouteKey = routeKey;
        Label = label;
        Icon = Geometry.Parse(iconData);
    }

    public string RouteKey { get; }

    public string Label { get; }

    public Geometry Icon { get; }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
