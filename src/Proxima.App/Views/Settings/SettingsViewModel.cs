using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.Views.Auth;
using Proxima.App.ViewModels;
using Proxima.Application.Settings;
using Proxima.Domain.Auth;

namespace Proxima.App.Views.Settings;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IRuntimeUserContext _runtimeUserContext;
    private readonly DelegateCommand _saveCommand;
    private readonly DelegateCommand _reloadCommand;
    private readonly DelegateCommand _changePasswordCommand;
    private readonly DelegateCommand _exportSnapshotCommand;
    private readonly DelegateCommand _importSnapshotCommand;

    private UserRole _selectedRole = UserRole.PrivateInvestor;
    private AppLanguage _selectedLanguage = AppLanguage.RU;
    private string _displayName = string.Empty;
    private string _login = "local";
    private string _preferredCurrency = "USD";
    private decimal _uiScale = 1m;
    private QuoteProviderKind _selectedQuoteProvider = QuoteProviderKind.Mock;
    private int _quoteRefreshMinutes = 15;
    private CurrencyProviderKind _selectedCurrencyProvider = CurrencyProviderKind.Mock;
    private bool _syncEnabled;
    private int _selectedSyncModeIndex;

    private bool _isLoading;
    private bool _isSaving;
    private bool _isSaved;
    private bool _hasError;
    private string _statusMessage = "Загрузка настроек...";
    private string _errorMessage = string.Empty;
    private string _securityStatus = "Локальный пароль включен.";
    private string _snapshotStatus = "Снапшоты еще не создавались.";

    public SettingsViewModel(ISettingsService settingsService, IRuntimeUserContext runtimeUserContext)
    {
        _settingsService = settingsService;
        _runtimeUserContext = runtimeUserContext;

        CurrencyOptions = ["USD", "EUR", "BYN", "RUB"];
        LanguageOptions = Enum.GetValues<AppLanguage>();
        RoleOptions = Enum.GetValues<UserRole>();
        QuoteProviderOptions = Enum.GetValues<QuoteProviderKind>();
        CurrencyProviderOptions = Enum.GetValues<CurrencyProviderKind>();
        ThemeOptions = ["Светлая"];
        SyncModeOptions = ["Выключена", "Включена"];

        _saveCommand = new DelegateCommand(_ => _ = SaveAsync());
        _reloadCommand = new DelegateCommand(_ => _ = LoadAsync());
        _changePasswordCommand = new DelegateCommand(_ => ChangePassword());
        _exportSnapshotCommand = new DelegateCommand(_ => ExportSnapshot());
        _importSnapshotCommand = new DelegateCommand(_ => ImportSnapshot());

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
    public IReadOnlyList<string> SyncModeOptions { get; }

    public ICommand SaveCommand => _saveCommand;

    public ICommand ReloadCommand => _reloadCommand;

    public ICommand ChangePasswordCommand => _changePasswordCommand;

    public ICommand ExportSnapshotCommand => _exportSnapshotCommand;

    public ICommand ImportSnapshotCommand => _importSnapshotCommand;

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
        set => SetProperty(ref _preferredCurrency, value);
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

    public bool SyncEnabled
    {
        get => _syncEnabled;
        private set => SetProperty(ref _syncEnabled, value);
    }

    public int SelectedSyncModeIndex
    {
        get => _selectedSyncModeIndex;
        set
        {
            if (SetProperty(ref _selectedSyncModeIndex, value))
            {
                SyncEnabled = value == 1;
            }
        }
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
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string SecurityStatus
    {
        get => _securityStatus;
        private set => SetProperty(ref _securityStatus, value);
    }

    public string SnapshotStatus
    {
        get => _snapshotStatus;
        private set => SetProperty(ref _snapshotStatus, value);
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
        return new SettingsViewModel(new DesignSettingsService(), context);
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
                PreferredCurrency,
                SelectedLanguage,
                UiScale,
                SelectedQuoteProvider,
                QuoteRefreshMinutes,
                null,
                SelectedCurrencyProvider,
                SyncEnabled)).ConfigureAwait(true);

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

    private void ExportSnapshot()
    {
        StatusMessage = "Экспорт снапшота будет подключен через Proxima.Sync.";
    }

    private void ImportSnapshot()
    {
        StatusMessage = "Импорт снапшота будет подключен через Proxima.Sync.";
    }

    private void Apply(UserSettings settings)
    {
        DisplayName = settings.DisplayName;
        Login = settings.Login;
        SelectedRole = settings.Role;
        PreferredCurrency = settings.PreferredCurrency;
        SelectedLanguage = settings.Language;
        UiScale = settings.UiScale;
        SelectedQuoteProvider = settings.QuoteProvider;
        QuoteRefreshMinutes = settings.QuoteRefreshMinutes;
        SelectedCurrencyProvider = settings.CurrencyProvider;
        SyncEnabled = settings.SyncEnabled;
        SelectedSyncModeIndex = settings.SyncEnabled ? 1 : 0;

        SnapshotStatus = settings.LastSnapshotAt is null
            ? "Снапшоты еще не создавались."
            : $"Последний снапшот: {settings.LastSnapshotAt:yyyy-MM-dd HH:mm}";
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
                QuoteProviderKind.Mock,
                15,
                string.Empty,
                CurrencyProviderKind.Mock,
                false,
                null));
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
                request.PreferredCurrency,
                request.Language,
                request.UiScale,
                request.QuoteProvider,
                request.QuoteRefreshMinutes,
                string.Empty,
                request.CurrencyProvider,
                request.SyncEnabled,
                DateTimeOffset.UtcNow);

            return Task.FromResult(SettingsOperationResult.Success(updated));
        }
    }
}
