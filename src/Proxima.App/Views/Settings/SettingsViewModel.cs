using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.Notifications;
using Proxima.App.ViewModels;
using Proxima.App.Views.Auth;
using Proxima.Core.Application.Settings;
using Proxima.Core.Domain.Auth;

namespace Proxima.App.Views.Settings;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IRuntimeUserContext _runtimeUserContext;
    private readonly IAppNotificationCenter _notificationCenter;
    private readonly DelegateCommand _saveCommand;
    private readonly DelegateCommand _reloadCommand;
    private readonly DelegateCommand _changePasswordCommand;

    private QuoteProviderKind _selectedQuoteProvider = QuoteProviderKind.TwelveData;
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

    public IReadOnlyList<QuoteProviderKind> QuoteProviderOptions { get; }

    public IReadOnlyList<CurrencyProviderKind> CurrencyProviderOptions { get; }

    public IReadOnlyList<string> ThemeOptions { get; }

    public ICommand SaveCommand => _saveCommand;

    public ICommand ReloadCommand => _reloadCommand;

    public ICommand ChangePasswordCommand => _changePasswordCommand;

    public QuoteProviderKind SelectedQuoteProvider
    {
        get => _selectedQuoteProvider;
        set => SetProperty(ref _selectedQuoteProvider, value);
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
                _runtimeUserContext.UserId)).ConfigureAwait(true);

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
                SelectedQuoteProvider,
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
        SelectedQuoteProvider = QuoteProviderKind.TwelveData;
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

}
