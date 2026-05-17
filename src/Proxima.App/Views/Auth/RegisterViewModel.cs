using System.Windows.Input;
using Proxima.App.ViewModels;
using Proxima.Core.Application.Auth;
using Proxima.Core.Domain.Auth;

namespace Proxima.App.Views.Auth;

public sealed class RegisterViewModel : ViewModelBase
{
    private readonly ILocalAuthService _authService;
    private readonly AsyncCommand _registerCommand;
    private readonly DelegateCommand _navigateToLoginCommand;

    private string _login = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public RegisterViewModel(ILocalAuthService authService)
    {
        _authService = authService;
        _registerCommand = new AsyncCommand(RegisterAsync, () => CanSubmit);
        _navigateToLoginCommand = new DelegateCommand(_ => LoginRequested?.Invoke(this, EventArgs.Empty), _ => IsNotBusy);
    }

    public event EventHandler<LocalUserProfile>? Registered;

    public event EventHandler? LoginRequested;

    public string Login
    {
        get => _login;
        set
        {
            if (SetProperty(ref _login, value))
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
                _registerCommand.RaiseCanExecuteChanged();
                _navigateToLoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool CanSubmit =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(Login)
        && !string.IsNullOrWhiteSpace(Password);

    public string PrimaryActionText => IsBusy ? "Регистрируем..." : "Зарегистрироваться  →";

    public ICommand RegisterCommand => _registerCommand;

    public ICommand NavigateToLoginCommand => _navigateToLoginCommand;

private async Task RegisterAsync()
    {
        if (!CanSubmit)
        {
            return;
        }

        IsBusy = true;
        try
        {
            string normalizedLogin = Login.Trim();
            string displayName = BuildDisplayName(normalizedLogin);

            AuthResult result = await _authService.CreateProfileAsync(new CreateProfileRequest(
                displayName,
                normalizedLogin,
                Password,
                Password,
                UserRole.PrivateInvestor)).ConfigureAwait(true);

            if (!result.Succeeded || result.Profile is null)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.UserMessage)
                    ? "Не удалось зарегистрироваться."
                    : result.UserMessage;
                return;
            }

            Password = string.Empty;
            Registered?.Invoke(this, result.Profile);
        }
        catch (Exception ex)
        {
            ErrorMessage = BuildRegistrationError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildRegistrationError(Exception ex)
    {
        Exception root = ex.GetBaseException();
        string message = string.IsNullOrWhiteSpace(root.Message) ? root.GetType().Name : root.Message;

        if (message.Contains("same login", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
            || message.Contains("уже", StringComparison.OrdinalIgnoreCase))
        {
            return "Пользователь с таким логином уже существует.";
        }

        return $"Не удалось зарегистрироваться: {message}";
    }

    private static string BuildDisplayName(string login)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return "Пользователь";
        }

        int emailSeparatorIndex = login.IndexOf('@', StringComparison.Ordinal);
        if (emailSeparatorIndex > 0)
        {
            return login[..emailSeparatorIndex].Trim();
        }

        return login.Trim();
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
        _registerCommand.RaiseCanExecuteChanged();
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
