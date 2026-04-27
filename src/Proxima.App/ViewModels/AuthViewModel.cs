using Proxima.Application.Auth;
using Proxima.Domain.Auth;

namespace Proxima.App.ViewModels;

public sealed class AuthViewModel(ILocalAuthService authService) : ViewModelBase
{
    private readonly PasswordPolicyValidator _passwordPolicy = new();
    private string _displayName = string.Empty;
    private string _login = string.Empty;
    private string _unlockLogin = string.Empty;
    private int _selectedRoleIndex;
    private bool _isSetupMode = true;
    private bool _isUnlocked;
    private bool _isBusy;
    private bool _showRecoveryInfo;
    private string _statusMessage = string.Empty;
    private string _validationMessage = string.Empty;
    private int _failedAttempts;

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
            {
                RefreshValidation();
            }
        }
    }

    public string Login
    {
        get => _login;
        set
        {
            if (SetProperty(ref _login, value))
            {
                RefreshValidation();
            }
        }
    }

    public string UnlockLogin
    {
        get => _unlockLogin;
        set
        {
            if (SetProperty(ref _unlockLogin, value))
            {
                OnPropertyChanged(nameof(CanUnlock));
            }
        }
    }

    public int SelectedRoleIndex
    {
        get => _selectedRoleIndex;
        set => SetProperty(ref _selectedRoleIndex, value);
    }

    public bool IsSetupMode
    {
        get => _isSetupMode;
        private set
        {
            if (SetProperty(ref _isSetupMode, value))
            {
                OnPropertyChanged(nameof(IsLoginMode));
            }
        }
    }

    public bool IsLoginMode => !IsSetupMode && !IsUnlocked;

    public bool IsUnlocked
    {
        get => _isUnlocked;
        private set
        {
            if (SetProperty(ref _isUnlocked, value))
            {
                OnPropertyChanged(nameof(IsLoginMode));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanCreateProfile));
                OnPropertyChanged(nameof(CanUnlock));
            }
        }
    }

    public bool ShowRecoveryInfo
    {
        get => _showRecoveryInfo;
        private set => SetProperty(ref _showRecoveryInfo, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public bool CanCreateProfile =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(DisplayName)
        && !string.IsNullOrWhiteSpace(Login)
        && string.IsNullOrWhiteSpace(ValidationMessage);

    public bool CanUnlock => !IsBusy && !string.IsNullOrWhiteSpace(UnlockLogin);

    public string FailedAttemptsMessage =>
        _failedAttempts >= 3
            ? "После нескольких неудачных попыток Proxima добавляет небольшую задержку перед проверкой."
            : string.Empty;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            IsSetupMode = await authService.NeedsFirstRunSetupAsync(cancellationToken).ConfigureAwait(true);
            StatusMessage = IsSetupMode
                ? "Создайте локальный профиль для этого устройства."
                : "Введите локальный пароль, чтобы разблокировать Proxima.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void PreviewSetupPassword(string password, string confirmation)
    {
        RefreshValidation(password, confirmation);
    }

    public async Task CreateProfileAsync(string password, string confirmation, CancellationToken cancellationToken = default)
    {
        RefreshValidation(password, confirmation);
        if (!CanCreateProfile)
        {
            return;
        }

        IsBusy = true;
        try
        {
            CreateProfileRequest request = new(
                DisplayName,
                Login,
                password,
                confirmation,
                SelectedRoleIndex == 1 ? UserRole.FinancialAnalyst : UserRole.PrivateInvestor);

            AuthResult result = await authService.CreateProfileAsync(request, cancellationToken).ConfigureAwait(true);
            if (result.Succeeded)
            {
                UnlockLogin = result.Profile!.Login;
                IsSetupMode = false;
                IsUnlocked = true;
                StatusMessage = "Профиль создан. Proxima разблокирована.";
                ValidationMessage = string.Empty;
                return;
            }

            StatusMessage = result.UserMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UnlockAsync(string password, CancellationToken cancellationToken = default)
    {
        if (!CanUnlock)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (_failedAttempts >= 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(true);
            }

            AuthResult result = await authService.UnlockAsync(UnlockLogin, password, cancellationToken).ConfigureAwait(true);
            if (result.Succeeded)
            {
                _failedAttempts = 0;
                IsUnlocked = true;
                StatusMessage = "Proxima разблокирована.";
                OnPropertyChanged(nameof(FailedAttemptsMessage));
                return;
            }

            if (result.FailureReason == AuthFailureReason.GenericAuthenticationFailed)
            {
                _failedAttempts++;
            }

            StatusMessage = result.UserMessage;
            OnPropertyChanged(nameof(FailedAttemptsMessage));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ToggleRecoveryInfo()
    {
        ShowRecoveryInfo = !ShowRecoveryInfo;
    }

    private void RefreshValidation(string password = "", string confirmation = "")
    {
        if (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(confirmation))
        {
            ValidationMessage = string.Empty;
        }
        else
        {
            PasswordValidationResult result = _passwordPolicy.Validate(password, confirmation);
            ValidationMessage = result.IsValid ? string.Empty : string.Join(Environment.NewLine, result.Errors);
        }

        OnPropertyChanged(nameof(CanCreateProfile));
    }
}
