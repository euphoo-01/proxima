using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.Navigation;
using Proxima.App.ViewModels;
using Proxima.App.Views.Auth;
using Proxima.Application.Portfolios;
using Proxima.Domain.Auth;
using Proxima.Domain.Portfolios;

namespace Proxima.App.Shell;

public sealed class TopbarViewModel : ViewModelBase
{
    private readonly IPortfolioService _portfolioService;
    private readonly IRuntimeUserContext _userContext;
    private readonly IShellPortfolioCoordinator _portfolioCoordinator;
    private readonly IShellState _shellState;
    private readonly IAppNavigationService _navigation;
    private readonly AsyncCommand _createPortfolioCommand;
    private readonly DelegateCommand _notificationsCommand;
    private string _title = "Дешборд";
    private string _breadcrumb = "Дешборд";
    private string _currentPortfolio = "Основное портфолио";
    private PortfolioOption? _selectedPortfolio;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public TopbarViewModel(
        IPortfolioService portfolioService,
        IRuntimeUserContext userContext,
        IShellPortfolioCoordinator portfolioCoordinator,
        IShellState shellState,
        IAppNavigationService navigation)
    {
        _portfolioService = portfolioService;
        _userContext = userContext;
        _portfolioCoordinator = portfolioCoordinator;
        _shellState = shellState;
        _navigation = navigation;
        _createPortfolioCommand = new AsyncCommand(CreatePortfolioAsync, () => CanCreatePortfolio && !IsBusy);
        _notificationsCommand = new DelegateCommand(_ => _navigation.Navigate(AppRoutes.Notifications));

        Portfolios = [];
        _ = InitializeAsync();
    }

    public ObservableCollection<PortfolioOption> Portfolios { get; }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Breadcrumb
    {
        get => _breadcrumb;
        private set => SetProperty(ref _breadcrumb, value);
    }

    public string CurrentPortfolio
    {
        get => _currentPortfolio;
        private set => SetProperty(ref _currentPortfolio, NormalizePortfolioName(value));
    }

    public PortfolioOption? SelectedPortfolio
    {
        get => _selectedPortfolio;
        set
        {
            if (SetProperty(ref _selectedPortfolio, value) && value is not null)
            {
                CurrentPortfolio = value.Name;
                _portfolioCoordinator.SetCurrentPortfolio(value.Id, value.Name, _shellState.CurrentPortfolioValue);
            }
        }
    }

    public bool IsFinancialConsultant => _userContext.Role == UserRole.FinancialAnalyst;

    public bool ShowPortfolioSelector => IsFinancialConsultant;

    public bool ShowPortfolioText => !ShowPortfolioSelector;

    public bool CanCreatePortfolio => IsFinancialConsultant;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _createPortfolioCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand CreatePortfolioCommand => _createPortfolioCommand;

    public ICommand NotificationsCommand => _notificationsCommand;

    public void Update(string title, string breadcrumb, string currentPortfolio)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Дешборд" : title;
        Breadcrumb = string.IsNullOrWhiteSpace(breadcrumb) ? Title : breadcrumb;

        if (SelectedPortfolio is null)
        {
            CurrentPortfolio = currentPortfolio;
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty)
        {
            return;
        }

        await ReloadPortfoliosAsync(selectCurrent: true, cancellationToken).ConfigureAwait(true);
    }

    private async Task ReloadPortfoliosAsync(bool selectCurrent, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            IReadOnlyList<Portfolio> portfolios = await _portfolioService.ListActiveAsync(_userContext.UserId, cancellationToken).ConfigureAwait(true);

            if (portfolios.Count == 0)
            {
                PortfolioOperationResult result = await _portfolioService.CreateAsync(
                    new CreatePortfolioRequest(_userContext.UserId, "Основное портфолио", "USD", null, null),
                    cancellationToken).ConfigureAwait(true);

                if (result.Succeeded && result.Portfolio is not null)
                {
                    portfolios = [result.Portfolio];
                }
            }

            Portfolios.Clear();
            foreach (Portfolio portfolio in portfolios.OrderBy(static portfolio => portfolio.Name, StringComparer.OrdinalIgnoreCase))
            {
                Portfolios.Add(new PortfolioOption(portfolio.Id, NormalizePortfolioName(portfolio.Name), portfolio.BaseCurrency, portfolio.Description, portfolio.ClientLabel));
            }

            if (!selectCurrent || Portfolios.Count == 0)
            {
                return;
            }

            PortfolioOption? selected = Portfolios.FirstOrDefault(item => item.Id == _shellState.CurrentPortfolioId)
                ?? Portfolios.FirstOrDefault();

            if (selected is not null)
            {
                SelectedPortfolio = selected;
                CurrentPortfolio = selected.Name;
                _portfolioCoordinator.SetCurrentPortfolio(selected.Id, selected.Name, _shellState.CurrentPortfolioValue);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CreatePortfolioAsync()
    {
        if (!CanCreatePortfolio || _userContext.UserId == Guid.Empty)
        {
            return;
        }

        IsBusy = true;
        try
        {
            int nextNumber = Portfolios.Count + 1;
            string name = $"Портфель {nextNumber}";
            while (Portfolios.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                nextNumber++;
                name = $"Портфель {nextNumber}";
            }

            PortfolioOperationResult result = await _portfolioService.CreateAsync(
                new CreatePortfolioRequest(_userContext.UserId, name, "USD", null, null),
                CancellationToken.None).ConfigureAwait(true);

            if (!result.Succeeded || result.Portfolio is null)
            {
                StatusMessage = string.IsNullOrWhiteSpace(result.Message) ? "Не удалось создать портфель." : result.Message;
                return;
            }

            Portfolio portfolio = result.Portfolio;
            PortfolioOption option = new(portfolio.Id, portfolio.Name, portfolio.BaseCurrency, portfolio.Description, portfolio.ClientLabel);
            Portfolios.Add(option);
            SelectedPortfolio = option;
            StatusMessage = "Портфель создан.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string NormalizePortfolioName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("Demo Portfolio", StringComparison.OrdinalIgnoreCase))
        {
            return "Основное портфолио";
        }

        return value.Trim();
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
