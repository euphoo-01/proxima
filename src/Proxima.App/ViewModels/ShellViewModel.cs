using System.Collections.ObjectModel;
using Proxima.Application.Portfolios;
using Proxima.Domain.Portfolios;

namespace Proxima.App.ViewModels;

public sealed class ShellViewModel : ViewModelBase
{
    private readonly ShellNavigationService _navigation;
    private readonly IPortfolioService _portfolios;
    private Guid _ownerUserId;

    private string _activeRoute = "dashboard";
    private string _breadcrumb = "Дешборд";
    private string _pageTitle = "Дешборд";
    private string _pageDescription = "Обзор состояния портфеля и последних изменений.";
    private string _statusText = "Локальный режим · Котировки из кэша";

    private bool _isCreatePortfolioDialogOpen;
    private string _newPortfolioName = string.Empty;
    private string _newPortfolioCurrency = "USD";
    private string _newPortfolioDescription = string.Empty;
    private string _newPortfolioClientLabel = string.Empty;
    private string _createPortfolioValidation = string.Empty;
    private PortfolioOption? _selectedPortfolio;

    private bool _isManagePortfolioDialogOpen;
    private string _editPortfolioName = string.Empty;
    private string _editPortfolioCurrency = "USD";
    private string _editPortfolioDescription = string.Empty;
    private string _editPortfolioClientLabel = string.Empty;
    private string _managePortfolioValidation = string.Empty;

    public ShellViewModel(ShellNavigationService navigation, IPortfolioService portfolios)
    {
        _navigation = navigation;
        _portfolios = portfolios;
        RegisterRoutes();
        ApplyRoute(_navigation.Navigate("dashboard", pushHistory: false));
    }

    public ObservableCollection<PortfolioOption> PortfolioOptions { get; } = [];

    public IEnumerable<string> Currencies => new[] { "USD", "EUR", "BYN", "RUB" };

    public string ActiveRoute
    {
        get => _activeRoute;
        private set => SetProperty(ref _activeRoute, value);
    }

    public string Breadcrumb
    {
        get => _breadcrumb;
        private set => SetProperty(ref _breadcrumb, value);
    }

    public string PageTitle
    {
        get => _pageTitle;
        private set => SetProperty(ref _pageTitle, value);
    }

    public string PageDescription
    {
        get => _pageDescription;
        private set => SetProperty(ref _pageDescription, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsCreatePortfolioDialogOpen
    {
        get => _isCreatePortfolioDialogOpen;
        private set => SetProperty(ref _isCreatePortfolioDialogOpen, value);
    }

    public string NewPortfolioName
    {
        get => _newPortfolioName;
        set
        {
            if (SetProperty(ref _newPortfolioName, value))
            {
                ValidateNewPortfolio();
                OnPropertyChanged(nameof(CanCreatePortfolio));
            }
        }
    }

    public string NewPortfolioCurrency
    {
        get => _newPortfolioCurrency;
        set
        {
            if (SetProperty(ref _newPortfolioCurrency, value))
            {
                ValidateNewPortfolio();
            }
        }
    }

    public string NewPortfolioDescription
    {
        get => _newPortfolioDescription;
        set => SetProperty(ref _newPortfolioDescription, value);
    }

    public string NewPortfolioClientLabel
    {
        get => _newPortfolioClientLabel;
        set => SetProperty(ref _newPortfolioClientLabel, value);
    }

    public string CreatePortfolioValidation
    {
        get => _createPortfolioValidation;
        private set => SetProperty(ref _createPortfolioValidation, value);
    }

    public bool CanCreatePortfolio =>
        !string.IsNullOrWhiteSpace(NewPortfolioName)
        && string.IsNullOrWhiteSpace(CreatePortfolioValidation);

    public PortfolioOption? SelectedPortfolio
    {
        get => _selectedPortfolio;
        set
        {
            if (SetProperty(ref _selectedPortfolio, value) && value is not null)
            {
                HandlePortfolioSwitch();
            }
        }
    }

    public bool IsManagePortfolioDialogOpen
    {
        get => _isManagePortfolioDialogOpen;
        private set => SetProperty(ref _isManagePortfolioDialogOpen, value);
    }

    public string EditPortfolioName
    {
        get => _editPortfolioName;
        set
        {
            if (SetProperty(ref _editPortfolioName, value))
            {
                ValidateManagePortfolio();
                OnPropertyChanged(nameof(CanSavePortfolioChanges));
            }
        }
    }

    public string EditPortfolioCurrency
    {
        get => _editPortfolioCurrency;
        set
        {
            if (SetProperty(ref _editPortfolioCurrency, value))
            {
                ValidateManagePortfolio();
            }
        }
    }

    public string EditPortfolioDescription
    {
        get => _editPortfolioDescription;
        set => SetProperty(ref _editPortfolioDescription, value);
    }

    public string EditPortfolioClientLabel
    {
        get => _editPortfolioClientLabel;
        set => SetProperty(ref _editPortfolioClientLabel, value);
    }

    public string ManagePortfolioValidation
    {
        get => _managePortfolioValidation;
        private set => SetProperty(ref _managePortfolioValidation, value);
    }

    public bool CanSavePortfolioChanges =>
        SelectedPortfolio is not null
        && !string.IsNullOrWhiteSpace(EditPortfolioName)
        && string.IsNullOrWhiteSpace(ManagePortfolioValidation);

    public bool IsDashboardPage => ActiveRoute.Equals("dashboard", StringComparison.Ordinal);
    public bool IsAssetsPage => ActiveRoute.Equals("assets", StringComparison.Ordinal);
    public bool IsTaxesPage => ActiveRoute.Equals("taxes", StringComparison.Ordinal);
    public bool IsGoalsPage => ActiveRoute.Equals("goals", StringComparison.Ordinal);
    public bool IsSettingsPage => ActiveRoute.Equals("settings", StringComparison.Ordinal);
    public bool IsAssetDetailsPage => ActiveRoute.Equals("assets/details", StringComparison.Ordinal);
    public bool IsManualImportPage => ActiveRoute.Equals("assets/import", StringComparison.Ordinal);

    public bool CanGoBack => _navigation.CanGoBack;

    public async Task InitializeAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        _ownerUserId = ownerUserId;
        await ReloadPortfoliosAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Navigate(string route)
    {
        ApplyRoute(_navigation.Navigate(route));
    }

    public void GoBack()
    {
        if (!_navigation.CanGoBack)
        {
            return;
        }

        ApplyRoute(_navigation.GoBack());
    }

    public void OpenAssetDetails()
    {
        Navigate("assets/details");
    }

    public void OpenManualImport()
    {
        Navigate("assets/import");
    }

    public void OpenCreatePortfolioDialog()
    {
        NewPortfolioName = string.Empty;
        NewPortfolioCurrency = "USD";
        NewPortfolioDescription = string.Empty;
        NewPortfolioClientLabel = string.Empty;
        CreatePortfolioValidation = string.Empty;
        IsCreatePortfolioDialogOpen = true;
    }

    public void CancelCreatePortfolioDialog()
    {
        IsCreatePortfolioDialogOpen = false;
    }

    public async Task CreatePortfolioAsync(CancellationToken cancellationToken = default)
    {
        ValidateNewPortfolio();
        if (!CanCreatePortfolio)
        {
            return;
        }

        PortfolioOperationResult result = await _portfolios.CreateAsync(new CreatePortfolioRequest(
            _ownerUserId,
            NewPortfolioName,
            NewPortfolioCurrency,
            NewPortfolioDescription,
            NewPortfolioClientLabel), cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            CreatePortfolioValidation = result.Message;
            return;
        }

        await ReloadPortfoliosAsync(cancellationToken, preferPortfolioId: result.Portfolio!.Id).ConfigureAwait(false);
        StatusText = $"Текущий портфель: {SelectedPortfolio!.Name} ({SelectedPortfolio.Currency})";
        IsCreatePortfolioDialogOpen = false;
    }

    public void OpenManagePortfolioDialog()
    {
        if (SelectedPortfolio is null)
        {
            return;
        }

        EditPortfolioName = SelectedPortfolio.Name;
        EditPortfolioCurrency = SelectedPortfolio.Currency;
        EditPortfolioDescription = SelectedPortfolio.Description ?? string.Empty;
        EditPortfolioClientLabel = SelectedPortfolio.ClientLabel ?? string.Empty;
        ManagePortfolioValidation = string.Empty;
        IsManagePortfolioDialogOpen = true;
    }

    public void CancelManagePortfolioDialog()
    {
        IsManagePortfolioDialogOpen = false;
    }

    public async Task SavePortfolioChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateManagePortfolio();
        if (!CanSavePortfolioChanges || SelectedPortfolio is null)
        {
            return;
        }

        PortfolioOperationResult result = await _portfolios.UpdateAsync(new UpdatePortfolioRequest(
            _ownerUserId,
            SelectedPortfolio.Id,
            EditPortfolioName,
            EditPortfolioCurrency,
            EditPortfolioDescription,
            EditPortfolioClientLabel), cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            ManagePortfolioValidation = result.Message;
            return;
        }

        await ReloadPortfoliosAsync(cancellationToken, preferPortfolioId: result.Portfolio!.Id).ConfigureAwait(false);
        StatusText = $"Портфель обновлён: {SelectedPortfolio!.Name} ({SelectedPortfolio.Currency})";
        IsManagePortfolioDialogOpen = false;
    }

    public async Task ArchiveSelectedPortfolioAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedPortfolio is null)
        {
            return;
        }

        PortfolioOperationResult result = await _portfolios.ArchiveAsync(_ownerUserId, SelectedPortfolio.Id, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            ManagePortfolioValidation = result.Message;
            return;
        }

        await ReloadPortfoliosAsync(cancellationToken).ConfigureAwait(false);
        IsManagePortfolioDialogOpen = false;

        if (SelectedPortfolio is null)
        {
            StatusText = "Нет активного портфеля. Создайте новый портфель.";
            return;
        }

        StatusText = $"Текущий портфель: {SelectedPortfolio.Name} ({SelectedPortfolio.Currency})";
    }

    private void RegisterRoutes()
    {
        _navigation.Register(new ShellRoute("dashboard", ShellPage.Dashboard, "Дешборд", "Дешборд"));
        _navigation.Register(new ShellRoute("assets", ShellPage.Assets, "Все активы", "Все активы"));
        _navigation.Register(new ShellRoute("assets/details", ShellPage.AssetDetails, "Детали актива", "Все активы / Детали"));
        _navigation.Register(new ShellRoute("assets/import", ShellPage.ManualImport, "Ручной импорт", "Все активы / Импорт"));
        _navigation.Register(new ShellRoute("taxes", ShellPage.Taxes, "Налоги", "Налоги"));
        _navigation.Register(new ShellRoute("goals", ShellPage.Goals, "Цели", "Цели"));
        _navigation.Register(new ShellRoute("settings", ShellPage.Settings, "Настройки", "Настройки"));
    }

    private void ApplyRoute(ShellRoute route)
    {
        ActiveRoute = route.Route;
        Breadcrumb = route.Breadcrumb;
        PageTitle = route.Title;
        PageDescription = route.Page switch
        {
            ShellPage.Dashboard => "Ключевые показатели портфеля и последние транзакции.",
            ShellPage.Assets => "Список активов, фильтры и быстрые действия.",
            ShellPage.AssetDetails => "Детальная аналитика выбранного актива.",
            ShellPage.ManualImport => "Ручной импорт операций и сверка строк.",
            ShellPage.Taxes => "Черновик налогового расчёта для РБ.",
            ShellPage.Goals => "Финансовые цели и прогноз накоплений.",
            ShellPage.Settings => "Параметры приложения и профиля.",
            _ => "Раздел Proxima.",
        };

        OnPropertyChanged(nameof(IsDashboardPage));
        OnPropertyChanged(nameof(IsAssetsPage));
        OnPropertyChanged(nameof(IsTaxesPage));
        OnPropertyChanged(nameof(IsGoalsPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(IsAssetDetailsPage));
        OnPropertyChanged(nameof(IsManualImportPage));
        OnPropertyChanged(nameof(CanGoBack));
    }

    private void ValidateNewPortfolio()
    {
        if (string.IsNullOrWhiteSpace(NewPortfolioName))
        {
            CreatePortfolioValidation = "Введите имя портфеля.";
            return;
        }

        if (PortfolioOptions.Any(portfolio =>
                string.Equals(portfolio.Name, NewPortfolioName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            CreatePortfolioValidation = "Портфель с таким именем уже есть. Выберите другое имя.";
            return;
        }

        CreatePortfolioValidation = string.Empty;
    }

    private void ValidateManagePortfolio()
    {
        if (string.IsNullOrWhiteSpace(EditPortfolioName))
        {
            ManagePortfolioValidation = "Название портфеля обязательно.";
            return;
        }

        if (SelectedPortfolio is not null
            && PortfolioOptions.Any(portfolio =>
                portfolio.Id != SelectedPortfolio.Id
                && string.Equals(portfolio.Name, EditPortfolioName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            ManagePortfolioValidation = "Портфель с таким именем уже есть.";
            return;
        }

        ManagePortfolioValidation = string.Empty;
    }

    private void HandlePortfolioSwitch()
    {
        if (SelectedPortfolio is null)
        {
            return;
        }

        if (IsAssetDetailsPage)
        {
            StatusText = "Текущий актив не принадлежит выбранному портфелю. Открыт безопасный список активов.";
            Navigate("assets");
            return;
        }

        StatusText = $"Текущий портфель: {SelectedPortfolio.Name} ({SelectedPortfolio.Currency})";
    }

    private async Task ReloadPortfoliosAsync(CancellationToken cancellationToken, Guid? preferPortfolioId = null)
    {
        IReadOnlyList<Portfolio> portfolios = await _portfolios.ListActiveAsync(_ownerUserId, cancellationToken).ConfigureAwait(false);

        PortfolioOptions.Clear();
        foreach (Portfolio portfolio in portfolios)
        {
            PortfolioOptions.Add(new PortfolioOption(
                portfolio.Id,
                portfolio.Name,
                portfolio.BaseCurrency,
                portfolio.Description,
                portfolio.ClientLabel));
        }

        PortfolioOption? selected = null;
        if (preferPortfolioId is not null)
        {
            selected = PortfolioOptions.FirstOrDefault(portfolio => portfolio.Id == preferPortfolioId.Value);
        }

        selected ??= PortfolioOptions.FirstOrDefault(portfolio => portfolio.Id == SelectedPortfolio?.Id);
        selected ??= PortfolioOptions.FirstOrDefault();

        SelectedPortfolio = selected;
        OnPropertyChanged(nameof(PortfolioOptions));
    }
}
