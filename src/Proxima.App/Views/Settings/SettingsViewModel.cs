using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.Views.Auth;
using Proxima.App.Notifications;
using Proxima.App.ViewModels;
using Proxima.Application.Settings;
using Proxima.Domain.Auth;

namespace Proxima.App.Views.Settings;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IRuntimeUserContext _runtimeUserContext;
    private readonly IAppNotificationCenter _notificationCenter;
    private readonly DelegateCommand _saveCommand;
    private readonly DelegateCommand _reloadCommand;
    private readonly DelegateCommand _changePasswordCommand;

    private UserRole _selectedRole = UserRole.PrivateInvestor;
    private AppLanguage _selectedLanguage = AppLanguage.RU;
    private string _displayName = string.Empty;
    private string _login = "local";
    private string _preferredCurrency = "USD";
    private decimal _uiScale = 1m;
    private QuoteProviderKind _selectedQuoteProvider = QuoteProviderKind.TwelveData;
    private int _quoteRefreshMinutes = 15;
    private CurrencyProviderKind _selectedCurrencyProvider = CurrencyProviderKind.Mock;

    private bool _isLoading;
    private bool _isSaving;
    private bool _isSaved;
    private bool _hasError;
    private string _statusMessage = "Загрузка настроек...";
    private string _errorMessage = string.Empty;
    private string _securityStatus = "Локальный пароль включен.";

    public SettingsViewModel(ISettingsService settingsService, IRuntimeUserContext runtimeUserContext, IAppNotificationCenter notificationCenter)
    {
        _settingsService = settingsService;
        _runtimeUserContext = runtimeUserContext;
        _notificationCenter = notificationCenter;

        CurrencyOptions = ["USD"];
        LanguageOptions = Enum.GetValues<AppLanguage>();
        RoleOptions = Enum.GetValues<UserRole>();
        QuoteProviderOptions = [QuoteProviderKind.TwelveData];
        CurrencyProviderOptions = Enum.GetValues<CurrencyProviderKind>();
        ThemeOptions = ["Светлая"];

        _saveCommand = new DelegateCommand(_ => _ = SaveAsync());
        _reloadCommand = new DelegateCommand(_ => _ = LoadAsync());
        _changePasswordCommand = new DelegateCommand(_ => ChangePassword());

        _ = LoadAsync();
    }

    public string PageTitle => "Настройки";

    public string BreadcrumbText => "Настройки";

    public IReadOnlyList<string> CurrencyOptions { get; }

    public IReadOnlyList<AppLanguage> LanguageOptions { get; }

    public IReadOnlyList<UserRole> RoleOptions { get; }

    public IReadOnlyList<QuoteProviderKind> QuoteProviderOptions { get; }

    public IReadOnlyList<CurrencyProviderKind> CurrencyProviderOptions { get; }

    public IReadOnlyList<string> ThemeOptions { get; }
    public ICommand SaveCommand => _saveCommand;

    public ICommand ReloadCommand => _reloadCommand;

    public ICommand ChangePasswordCommand => _changePasswordCommand;

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string Login
    {
        get => _login;
        private set => SetProperty(ref _login, value);
    }

    public UserRole SelectedRole
    {
        get => _selectedRole;
        set => SetProperty(ref _selectedRole, value);
    }

    public string PreferredCurrency
    {
        get => _preferredCurrency;
        set => SetProperty(ref _preferredCurrency, "USD");
    }

    public AppLanguage SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public decimal UiScale
    {
        get => _uiScale;
        set => SetProperty(ref _uiScale, value);
    }

    public QuoteProviderKind SelectedQuoteProvider
    {
        get => _selectedQuoteProvider;
        set => SetProperty(ref _selectedQuoteProvider, value);
    }

    public int QuoteRefreshMinutes
    {
        get => _quoteRefreshMinutes;
        set => SetProperty(ref _quoteRefreshMinutes, value);
    }

    public CurrencyProviderKind SelectedCurrencyProvider
    {
        get => _selectedCurrencyProvider;
        set => SetProperty(ref _selectedCurrencyProvider, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(HasContent));
            }
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set => SetProperty(ref _isSaving, value);
    }

    public bool IsSaved
    {
        get => _isSaved;
        private set => SetProperty(ref _isSaved, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(HasContent));
            }
        }
    }

    public bool HasContent => !IsLoading && !HasError;

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value) && !string.IsNullOrWhiteSpace(value) && value.Contains("сохран", StringComparison.OrdinalIgnoreCase))
            {
                _ = _notificationCenter.NotifyAsync(AppNotificationLevel.Success, "Настройки сохранены", value, "Настройки");
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value) && !string.IsNullOrWhiteSpace(value))
            {
                _ = _notificationCenter.NotifyAsync(AppNotificationLevel.Error, "Ошибка настроек", value, "Настройки");
            }
        }
    }

    public string SecurityStatus
    {
        get => _securityStatus;
        private set => SetProperty(ref _securityStatus, value);
    }

    public static SettingsViewModel CreateDesignData()
    {
        RuntimeUserContext context = new();
        context.SetAuthenticated(new Proxima.Domain.Auth.LocalUserProfile(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Андрей К.",
            "local",
            UserRole.PrivateInvestor,
            new Proxima.Domain.Auth.PasswordCredential("design", [], [], 1, 1),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            0));
        return new SettingsViewModel(new DesignSettingsService(), context, new NoOpAppNotificationCenter());
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        IsSaved = false;
        ErrorMessage = string.Empty;

        try
        {
            if (!_runtimeUserContext.IsAuthenticated)
            {
                HasError = true;
                ErrorMessage = "Пользователь не авторизован. Перезапустите вход.";
                return;
            }

            UserSettings settings = await _settingsService.EnsureAsync(new CreateDefaultSettingsRequest(
                _runtimeUserContext.UserId,
                _runtimeUserContext.DisplayName,
                _runtimeUserContext.Role,
                _runtimeUserContext.Login,
                "USD")).ConfigureAwait(true);

            Apply(settings);
            StatusMessage = "Настройки загружены.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        IsSaving = true;
        IsSaved = false;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            if (!_runtimeUserContext.IsAuthenticated)
            {
                HasError = true;
                ErrorMessage = "Пользователь не авторизован. Перезапустите вход.";
                return;
            }

            SettingsOperationResult result = await _settingsService.UpdateAsync(new UpdateSettingsRequest(
                _runtimeUserContext.UserId,
                DisplayName,
                SelectedRole,
                "USD",
                SelectedLanguage,
                UiScale,
                SelectedQuoteProvider,
                QuoteRefreshMinutes,
                null,
                SelectedCurrencyProvider)).ConfigureAwait(true);

            if (!result.Succeeded || result.Settings is null)
            {
                HasError = true;
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message) ? "Не удалось сохранить настройки." : result.Message;
                return;
            }

            Apply(result.Settings);
            IsSaved = true;
            StatusMessage = "Настройки сохранены.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ChangePassword()
    {
        StatusMessage = "Смена пароля будет доступна после миграции auth в новый AppShell.";
    }

    private void Apply(UserSettings settings)
    {
        DisplayName = settings.DisplayName;
        Login = settings.Login;
        SelectedRole = settings.Role;
        PreferredCurrency = "USD";
        SelectedLanguage = settings.Language;
        UiScale = settings.UiScale;
        SelectedQuoteProvider = QuoteProviderKind.TwelveData;
        QuoteRefreshMinutes = settings.QuoteRefreshMinutes;
        SelectedCurrencyProvider = settings.CurrencyProvider;
    }

    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        private readonly Action<object?> _execute = execute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class DesignSettingsService : ISettingsService
    {
        public Task<UserSettings> EnsureAsync(CreateDefaultSettingsRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UserSettings(
                request.OwnerUserId,
                "Андрей К.",
                request.Role,
                request.Login,
                "USD",
                AppLanguage.RU,
                1m,
                QuoteProviderKind.TwelveData,
                15,
                string.Empty,
                CurrencyProviderKind.Mock));
        }

        public Task<UserSettings?> GetAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<UserSettings?>(null);
        }

        public Task<SettingsOperationResult> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken = default)
        {
            UserSettings updated = new(
                request.OwnerUserId,
                request.DisplayName,
                request.Role,
                "local",
                "USD",
                request.Language,
                request.UiScale,
                request.QuoteProvider,
                request.QuoteRefreshMinutes,
                string.Empty,
                request.CurrencyProvider);

            return Task.FromResult(SettingsOperationResult.Success(updated));
        }
    }
}
