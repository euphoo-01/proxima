using System.Collections.ObjectModel;

namespace Proxima.App.ViewModels;

public sealed class ShellViewModel : ViewModelBase
{
    private readonly ShellNavigationService _navigation;
    private string _activeRoute = "dashboard";
    private string _breadcrumb = "Дешборд";
    private string _pageTitle = "Дешборд";
    private string _pageDescription = "Обзор состояния портфеля и последних изменений.";
    private string _statusText = "Локальный режим · Котировки из кэша";
    private bool _isCreatePortfolioDialogOpen;
    private string _newPortfolioName = string.Empty;
    private string _newPortfolioCurrency = "USD";
    private string _createPortfolioValidation = string.Empty;
    private PortfolioOption? _selectedPortfolio;

    public ShellViewModel(ShellNavigationService navigation)
    {
        _navigation = navigation;
        RegisterRoutes();

        Portfolios = new ObservableCollection<PortfolioOption>
        {
            new(Guid.NewGuid(), "Личный портфель", "USD"),
            new(Guid.NewGuid(), "Дивиденды BYN", "BYN"),
        };
        _selectedPortfolio = Portfolios[0];
        ApplyRoute(_navigation.Navigate("dashboard", pushHistory: false));
    }

    public ObservableCollection<PortfolioOption> Portfolios { get; }

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

    public bool IsDashboardPage => ActiveRoute.Equals("dashboard", StringComparison.Ordinal);
    public bool IsAssetsPage => ActiveRoute.Equals("assets", StringComparison.Ordinal);
    public bool IsTaxesPage => ActiveRoute.Equals("taxes", StringComparison.Ordinal);
    public bool IsGoalsPage => ActiveRoute.Equals("goals", StringComparison.Ordinal);
    public bool IsSettingsPage => ActiveRoute.Equals("settings", StringComparison.Ordinal);
    public bool IsAssetDetailsPage => ActiveRoute.Equals("assets/details", StringComparison.Ordinal);
    public bool IsManualImportPage => ActiveRoute.Equals("assets/import", StringComparison.Ordinal);

    public bool CanGoBack => _navigation.CanGoBack;

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
        CreatePortfolioValidation = string.Empty;
        IsCreatePortfolioDialogOpen = true;
    }

    public void CancelCreatePortfolioDialog()
    {
        IsCreatePortfolioDialogOpen = false;
    }

    public void CreatePortfolio()
    {
        ValidateNewPortfolio();
        if (!CanCreatePortfolio)
        {
            return;
        }

        PortfolioOption newPortfolio = new(Guid.NewGuid(), NewPortfolioName.Trim(), NewPortfolioCurrency);
        Portfolios.Add(newPortfolio);
        SelectedPortfolio = newPortfolio;
        StatusText = $"Текущий портфель: {newPortfolio.Name} ({newPortfolio.Currency})";
        IsCreatePortfolioDialogOpen = false;
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

        if (Portfolios.Any(portfolio =>
                string.Equals(portfolio.Name, NewPortfolioName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            CreatePortfolioValidation = "Портфель с таким именем уже есть. Выберите другое имя.";
            return;
        }

        CreatePortfolioValidation = string.Empty;
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
}
