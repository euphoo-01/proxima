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

    private string _loginOrEmail = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public LoginViewModel(IAuthGateService authGateService)
    {
        _authGateService = authGateService;
        _unlockCommand = new AsyncCommand(UnlockAsync, () => CanSubmit);
        _forgotPasswordCommand = new DelegateCommand(_ => ShowRecoveryInfo(), _ => IsNotBusy);
    }

    public event EventHandler? Unlocked;

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
        private set => SetProperty(ref _errorMessage, value);
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
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool CanSubmit =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(LoginOrEmail)
        && !string.IsNullOrWhiteSpace(Password);

    public string PrimaryActionText => IsBusy ? "Unlocking..." : "Unlock";

    public ICommand UnlockCommand => _unlockCommand;

    public ICommand ForgotPasswordCommand => _forgotPasswordCommand;

    public static LoginViewModel CreateDesignData()
    {
        return new LoginViewModel(new InMemoryAuthGateService())
        {
            LoginOrEmail = "local",
            Password = string.Empty,
        };
    }

    private async Task UnlockAsync()
    {
        if (!CanSubmit)
        {
            return;
        }

        IsBusy = true;
        try
        {
            AuthGateResult result = await _authGateService.UnlockAsync(LoginOrEmail, Password).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
                OnPropertyChanged(nameof(HasError));
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
        ErrorMessage = "Recovery is unavailable in this temporary build. Recreate local profile if password is lost.";
        OnPropertyChanged(nameof(HasError));
    }

    private void ClearError()
    {
        if (string.IsNullOrEmpty(ErrorMessage))
        {
            return;
        }

        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(HasError));
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

public sealed class InMemoryAuthGateService : IAuthGateService
{
    private const int Iterations = 120_000;
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("proxima.local.auth.salt.v1");
    private static readonly byte[] SeedHash = ComputeHash("Proxima123!", Salt);

    public Task<AuthGateResult> UnlockAsync(string loginOrEmail, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalized = loginOrEmail.Trim().ToLowerInvariant();
        if (normalized is not ("local" or "local@proxima"))
        {
            return Task.FromResult(AuthGateResult.Fail("Invalid credentials."));
        }

        byte[] passwordHash = ComputeHash(password, Salt);
        bool isValid = CryptographicOperations.FixedTimeEquals(passwordHash, SeedHash);
        return Task.FromResult(isValid ? AuthGateResult.Success() : AuthGateResult.Fail("Invalid credentials."));
    }

    private static byte[] ComputeHash(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
    }
}
