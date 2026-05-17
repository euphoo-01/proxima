using System.Security.Cryptography;
using System.Text;
using System.Windows.Input;
using Proxima.App.ViewModels;

namespace Proxima.App.Views.Auth;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly IAuthGateService _authGateService;
    private readonly AsyncCommand _unlockCommand;
    private readonly DelegateCommand _forgotPasswordCommand;
    private readonly DelegateCommand _navigateToRegisterCommand;

    private string _loginOrEmail = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public LoginViewModel(IAuthGateService authGateService)
    {
        _authGateService = authGateService;
        _unlockCommand = new AsyncCommand(UnlockAsync, () => CanSubmit);
        _forgotPasswordCommand = new DelegateCommand(_ => ShowRecoveryInfo(), _ => IsNotBusy);
        _navigateToRegisterCommand = new DelegateCommand(_ => RegisterRequested?.Invoke(this, EventArgs.Empty), _ => IsNotBusy);
    }

    public event EventHandler? Unlocked;

    public event EventHandler? RegisterRequested;

    public string LoginOrEmail
    {
        get => _loginOrEmail;
        set
        {
            if (SetProperty(ref _loginOrEmail, value))
            {
                ClearError();
                RefreshCommands();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ClearError();
                RefreshCommands();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(CanSubmit));
                OnPropertyChanged(nameof(PrimaryActionText));
                _unlockCommand.RaiseCanExecuteChanged();
                _forgotPasswordCommand.RaiseCanExecuteChanged();
                _navigateToRegisterCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool CanSubmit =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(LoginOrEmail)
        && !string.IsNullOrWhiteSpace(Password);

    public string PrimaryActionText => IsBusy ? "Входим..." : "Войти  →";

    public ICommand UnlockCommand => _unlockCommand;

    public ICommand ForgotPasswordCommand => _forgotPasswordCommand;

    public ICommand NavigateToRegisterCommand => _navigateToRegisterCommand;

private async Task UnlockAsync()
    {
        if (!CanSubmit)
        {
            return;
        }

        IsBusy = true;
        try
        {
            AuthGateResult result = await _authGateService
                .UnlockAsync(LoginOrEmail, Password)
                .ConfigureAwait(true);

            if (!result.Succeeded)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Не удалось войти. Проверьте логин и пароль."
                    : result.ErrorMessage;
                return;
            }

            Password = string.Empty;
            Unlocked?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowRecoveryInfo()
    {
        ErrorMessage = "Восстановление пароля в локальной MVP-версии отключено. Если пароль утрачен, потребуется создать новый локальный профиль.";
    }

    private void ClearError()
    {
        if (!string.IsNullOrWhiteSpace(ErrorMessage))
        {
            ErrorMessage = string.Empty;
        }
    }

    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanSubmit));
        _unlockCommand.RaiseCanExecuteChanged();
    }

    private sealed class DelegateCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
    {
        private readonly Action<object?> _execute = execute;
        private readonly Func<object?, bool>? _canExecute = canExecute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class AsyncCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
    {
        private readonly Func<Task> _execute = execute;
        private readonly Func<bool> _canExecute = canExecute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute();

        public async void Execute(object? parameter)
        {
            await _execute().ConfigureAwait(true);
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public interface IAuthGateService
{
    Task<AuthGateResult> UnlockAsync(string loginOrEmail, string password, CancellationToken cancellationToken = default);
}

public sealed record AuthGateResult(bool Succeeded, string ErrorMessage)
{
    public static AuthGateResult Success() => new(true, string.Empty);

    public static AuthGateResult Fail(string message) => new(false, message);
}

