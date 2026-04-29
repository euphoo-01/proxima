using System.Collections.ObjectModel;
using Proxima.Application.Assets;
using Proxima.Application.Portfolios;
using Proxima.Application.Transactions;
using Proxima.Domain.Assets;
using Proxima.Domain.Portfolios;
using Proxima.Domain.Transactions;
using Proxima.Importing;

namespace Proxima.App.ViewModels;

public sealed class ShellViewModel : ViewModelBase
{
    private readonly ShellNavigationService _navigation;
    private readonly IPortfolioService _portfolios;
    private readonly IAssetService _assets;
    private readonly ITransactionService _transactions;
    private readonly IImportService _importService;
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
    private string _assetSearchQuery = string.Empty;
    private string _assetSort = "value_desc";
    private string _assetStatusText = "Активы не загружены.";
    private bool _isCreateAssetDialogOpen;
    private bool _isEditAssetDialogOpen;
    private string _assetValidation = string.Empty;
    private AssetRowViewModel? _selectedAsset;
    private string _assetTicker = string.Empty;
    private string _assetName = string.Empty;
    private string _assetCurrency = "USD";
    private string _assetExchange = string.Empty;
    private string _assetIsin = string.Empty;
    private string _assetTags = string.Empty;
    private string _assetNotes = string.Empty;
    private decimal _assetQuantity;
    private decimal _assetAverageBuyPrice;
    private decimal _assetCurrentPrice;
    private AssetType _assetType = AssetType.Stock;
    private string _transactionSearchQuery = string.Empty;
    private string _transactionSort = "date_desc";
    private string _transactionStatusText = "Транзакции не загружены.";
    private bool _isCreateTransactionDialogOpen;
    private bool _isEditTransactionDialogOpen;
    private string _transactionValidation = string.Empty;
    private TransactionRowViewModel? _selectedTransaction;
    private Guid? _transactionAssetId;
    private TransactionType _transactionType = TransactionType.Buy;
    private DateTimeOffset _transactionDate = DateTimeOffset.UtcNow;
    private decimal _transactionQuantity;
    private decimal _transactionPrice;
    private decimal _transactionGrossAmount;
    private decimal _transactionFeeAmount;
    private decimal _transactionTaxAmount;
    private string _transactionCurrency = "USD";
    private string _transactionBroker = string.Empty;
    private string _transactionExternalId = string.Empty;
    private string _transactionNotes = string.Empty;
    private bool _isImportDialogOpen;
    private string _importFilePath = string.Empty;
    private string _importMessage = string.Empty;
    private bool _importHasPdfLimitWarning;
    private bool _isImportPreviewVisible;

    public ShellViewModel(ShellNavigationService navigation, IPortfolioService portfolios, IAssetService assets, ITransactionService transactions, IImportService importService)
    {
        _navigation = navigation;
        _portfolios = portfolios;
        _assets = assets;
        _transactions = transactions;
        _importService = importService;
        RegisterRoutes();
        ApplyRoute(_navigation.Navigate("dashboard", pushHistory: false));
    }

    public ObservableCollection<PortfolioOption> PortfolioOptions { get; } = [];
    public ObservableCollection<AssetRowViewModel> Assets { get; } = [];
    public ObservableCollection<AssetRowViewModel> FilteredAssets { get; } = [];
    public ObservableCollection<TransactionRowViewModel> Transactions { get; } = [];
    public ObservableCollection<TransactionRowViewModel> FilteredTransactions { get; } = [];
    public ObservableCollection<ImportPreviewRowViewModel> ImportPreviewRows { get; } = [];

    public IEnumerable<string> Currencies => new[] { "USD", "EUR", "BYN", "RUB" };
    public IEnumerable<AssetType> AssetTypes => Enum.GetValues<AssetType>();
    public IEnumerable<TransactionType> TransactionTypes => Enum.GetValues<TransactionType>();

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

    public string AssetSearchQuery
    {
        get => _assetSearchQuery;
        set
        {
            if (SetProperty(ref _assetSearchQuery, value))
            {
                RefreshAssetTable();
            }
        }
    }

    public string AssetSort
    {
        get => _assetSort;
        set
        {
            if (SetProperty(ref _assetSort, value))
            {
                RefreshAssetTable();
            }
        }
    }

    public IEnumerable<string> AssetSortOptions => new[]
    {
        "value_desc",
        "name_asc",
        "pnl_desc",
    };

    public string AssetStatusText
    {
        get => _assetStatusText;
        private set => SetProperty(ref _assetStatusText, value);
    }

    public bool HasAssets => FilteredAssets.Count > 0;
    public bool IsAssetTableEmpty => !HasAssets;
    public bool IsCreateAssetDialogOpen
    {
        get => _isCreateAssetDialogOpen;
        private set => SetProperty(ref _isCreateAssetDialogOpen, value);
    }

    public bool IsEditAssetDialogOpen
    {
        get => _isEditAssetDialogOpen;
        private set => SetProperty(ref _isEditAssetDialogOpen, value);
    }

    public string AssetValidation
    {
        get => _assetValidation;
        private set => SetProperty(ref _assetValidation, value);
    }

    public AssetRowViewModel? SelectedAsset
    {
        get => _selectedAsset;
        set => SetProperty(ref _selectedAsset, value);
    }

    public string AssetTicker
    {
        get => _assetTicker;
        set => SetProperty(ref _assetTicker, value);
    }

    public string AssetName
    {
        get => _assetName;
        set => SetProperty(ref _assetName, value);
    }

    public string AssetCurrency
    {
        get => _assetCurrency;
        set => SetProperty(ref _assetCurrency, value);
    }

    public string AssetExchange
    {
        get => _assetExchange;
        set => SetProperty(ref _assetExchange, value);
    }

    public string AssetIsin
    {
        get => _assetIsin;
        set => SetProperty(ref _assetIsin, value);
    }

    public string AssetTags
    {
        get => _assetTags;
        set => SetProperty(ref _assetTags, value);
    }

    public string AssetNotes
    {
        get => _assetNotes;
        set => SetProperty(ref _assetNotes, value);
    }

    public decimal AssetQuantity
    {
        get => _assetQuantity;
        set => SetProperty(ref _assetQuantity, value);
    }

    public decimal AssetAverageBuyPrice
    {
        get => _assetAverageBuyPrice;
        set => SetProperty(ref _assetAverageBuyPrice, value);
    }

    public decimal AssetCurrentPrice
    {
        get => _assetCurrentPrice;
        set => SetProperty(ref _assetCurrentPrice, value);
    }

    public AssetType AssetType
    {
        get => _assetType;
        set => SetProperty(ref _assetType, value);
    }

    public string TransactionSearchQuery
    {
        get => _transactionSearchQuery;
        set
        {
            if (SetProperty(ref _transactionSearchQuery, value))
            {
                RefreshTransactionTable();
            }
        }
    }

    public string TransactionSort
    {
        get => _transactionSort;
        set
        {
            if (SetProperty(ref _transactionSort, value))
            {
                RefreshTransactionTable();
            }
        }
    }

    public IEnumerable<string> TransactionSortOptions => new[]
    {
        "date_desc",
        "amount_desc",
        "type_asc",
        "asset_asc",
    };

    public string TransactionStatusText
    {
        get => _transactionStatusText;
        private set => SetProperty(ref _transactionStatusText, value);
    }

    public bool HasTransactions => FilteredTransactions.Count > 0;
    public bool IsTransactionTableEmpty => !HasTransactions;

    public bool IsCreateTransactionDialogOpen
    {
        get => _isCreateTransactionDialogOpen;
        private set => SetProperty(ref _isCreateTransactionDialogOpen, value);
    }

    public bool IsEditTransactionDialogOpen
    {
        get => _isEditTransactionDialogOpen;
        private set => SetProperty(ref _isEditTransactionDialogOpen, value);
    }

    public string TransactionValidation
    {
        get => _transactionValidation;
        private set => SetProperty(ref _transactionValidation, value);
    }

    public TransactionRowViewModel? SelectedTransaction
    {
        get => _selectedTransaction;
        set => SetProperty(ref _selectedTransaction, value);
    }

    public Guid? TransactionAssetId
    {
        get => _transactionAssetId;
        set => SetProperty(ref _transactionAssetId, value);
    }

    public TransactionType TransactionType
    {
        get => _transactionType;
        set => SetProperty(ref _transactionType, value);
    }

    public DateTimeOffset TransactionDate
    {
        get => _transactionDate;
        set => SetProperty(ref _transactionDate, value);
    }

    public decimal TransactionQuantity
    {
        get => _transactionQuantity;
        set => SetProperty(ref _transactionQuantity, value);
    }

    public decimal TransactionPrice
    {
        get => _transactionPrice;
        set => SetProperty(ref _transactionPrice, value);
    }

    public decimal TransactionGrossAmount
    {
        get => _transactionGrossAmount;
        set => SetProperty(ref _transactionGrossAmount, value);
    }

    public decimal TransactionFeeAmount
    {
        get => _transactionFeeAmount;
        set => SetProperty(ref _transactionFeeAmount, value);
    }

    public decimal TransactionTaxAmount
    {
        get => _transactionTaxAmount;
        set => SetProperty(ref _transactionTaxAmount, value);
    }

    public string TransactionCurrency
    {
        get => _transactionCurrency;
        set => SetProperty(ref _transactionCurrency, value);
    }

    public string TransactionBroker
    {
        get => _transactionBroker;
        set => SetProperty(ref _transactionBroker, value);
    }

    public string TransactionExternalId
    {
        get => _transactionExternalId;
        set => SetProperty(ref _transactionExternalId, value);
    }

    public string TransactionNotes
    {
        get => _transactionNotes;
        set => SetProperty(ref _transactionNotes, value);
    }

    public bool IsImportDialogOpen
    {
        get => _isImportDialogOpen;
        private set => SetProperty(ref _isImportDialogOpen, value);
    }

    public string ImportFilePath
    {
        get => _importFilePath;
        set => SetProperty(ref _importFilePath, value);
    }

    public string ImportMessage
    {
        get => _importMessage;
        private set => SetProperty(ref _importMessage, value);
    }

    public bool ImportHasPdfLimitWarning
    {
        get => _importHasPdfLimitWarning;
        private set => SetProperty(ref _importHasPdfLimitWarning, value);
    }

    public bool IsImportPreviewVisible
    {
        get => _isImportPreviewVisible;
        private set => SetProperty(ref _isImportPreviewVisible, value);
    }

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
        if (SelectedAsset is null)
        {
            AssetStatusText = "Выберите актив в таблице.";
            return;
        }

        Navigate("assets/details");
    }

    public void OpenManualImport()
    {
        Navigate("assets/import");
    }

    public void OpenImportDialog()
    {
        ImportFilePath = string.Empty;
        ImportMessage = string.Empty;
        ImportHasPdfLimitWarning = false;
        IsImportPreviewVisible = false;
        ImportPreviewRows.Clear();
        IsImportDialogOpen = true;
    }

    public void CancelImportDialog()
    {
        IsImportDialogOpen = false;
    }

    public async Task PreviewImportAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ImportFilePath))
        {
            ImportMessage = "Укажите путь к файлу .csv или .pdf.";
            return;
        }

        ImportPreview preview = await _importService.PreviewAsync(ImportFilePath, cancellationToken).ConfigureAwait(false);
        ImportMessage = preview.Message;
        ImportHasPdfLimitWarning = preview.IsPdfLimited;
        ImportPreviewRows.Clear();
        foreach (ImportedTransactionRow row in preview.Rows)
        {
            ImportPreviewRows.Add(new ImportPreviewRowViewModel(row));
        }

        IsImportPreviewVisible = preview.Rows.Count > 0;
    }

    public async Task CommitImportAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedPortfolio is null)
        {
            ImportMessage = "Выберите портфель перед импортом.";
            return;
        }

        List<ImportPreviewRowViewModel> selectedRows = ImportPreviewRows.Where(static row => row.IsSelectedForCommit).ToList();
        if (selectedRows.Count == 0)
        {
            ImportMessage = "Нет выбранных строк для сохранения.";
            return;
        }

        Dictionary<string, AssetRowViewModel> byTicker = Assets.ToDictionary(static item => item.Ticker, StringComparer.OrdinalIgnoreCase);
        foreach (ImportPreviewRowViewModel row in selectedRows)
        {
            if (row.Source.Status == ImportRowStatus.Invalid)
            {
                ImportMessage = $"Строка {row.Source.RowNumber} содержит ошибки и не может быть сохранена.";
                return;
            }

            Guid? assetId = null;
            if (!string.IsNullOrWhiteSpace(row.Source.AssetTicker))
            {
                if (!byTicker.TryGetValue(row.Source.AssetTicker.Trim().ToUpperInvariant(), out AssetRowViewModel? asset))
                {
                    AssetOperationResult createdAsset = await _assets.CreateAsync(new CreateAssetRequest(
                        SelectedPortfolio.Id,
                        row.Source.AssetTicker,
                        string.IsNullOrWhiteSpace(row.Source.AssetName) ? row.Source.AssetTicker : row.Source.AssetName,
                        AssetType.Stock,
                        row.Source.Currency,
                        null,
                        null,
                        row.Source.Tag is null ? [] : [row.Source.Tag],
                        null,
                        0m,
                        0m,
                        0m), cancellationToken).ConfigureAwait(false);

                    if (!createdAsset.Succeeded || createdAsset.Asset is null)
                    {
                        ImportMessage = $"Не удалось создать актив для строки {row.Source.RowNumber}: {createdAsset.Message}";
                        return;
                    }

                    await ReloadAssetsAsync(cancellationToken).ConfigureAwait(false);
                    byTicker = Assets.ToDictionary(static item => item.Ticker, StringComparer.OrdinalIgnoreCase);
                    assetId = createdAsset.Asset.Id;
                }
                else
                {
                    assetId = asset.Id;
                }
            }

            TransactionOperationResult transaction = await _transactions.CreateAsync(new CreateTransactionRequest(
                SelectedPortfolio.Id,
                assetId,
                row.Source.TransactionType,
                row.Source.TradeDate,
                row.Source.Quantity,
                row.Source.Price,
                row.Source.GrossAmount,
                row.Source.FeeAmount,
                0m,
                row.Source.Currency,
                row.Source.Broker,
                null,
                $"import-row:{row.Source.RowNumber}"), cancellationToken).ConfigureAwait(false);

            if (!transaction.Succeeded)
            {
                ImportMessage = $"Сохранение прервано на строке {row.Source.RowNumber}: {transaction.Message}";
                return;
            }
        }

        await ReloadTransactionsAsync(cancellationToken).ConfigureAwait(false);
        ImportMessage = $"Импортировано строк: {selectedRows.Count}.";
        IsImportDialogOpen = false;
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
        RefreshTransactionTable();
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
        _ = ReloadAssetsAsync(CancellationToken.None);
        _ = ReloadTransactionsAsync(CancellationToken.None);
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
        await ReloadAssetsAsync(cancellationToken).ConfigureAwait(false);
        await ReloadTransactionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public void OpenCreateAssetDialog()
    {
        ResetAssetForm();
        IsCreateAssetDialogOpen = true;
    }

    public void CancelCreateAssetDialog()
    {
        IsCreateAssetDialogOpen = false;
    }

    public async Task CreateAssetAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedPortfolio is null)
        {
            AssetValidation = "Сначала выберите портфель.";
            return;
        }

        AssetOperationResult result = await _assets.CreateAsync(new CreateAssetRequest(
            SelectedPortfolio.Id,
            AssetTicker,
            AssetName,
            AssetType,
            AssetCurrency,
            AssetExchange,
            AssetIsin,
            ParseTags(AssetTags),
            AssetNotes,
            AssetQuantity,
            AssetAverageBuyPrice,
            AssetCurrentPrice), cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            AssetValidation = result.Message;
            return;
        }

        IsCreateAssetDialogOpen = false;
        await ReloadAssetsAsync(cancellationToken).ConfigureAwait(false);
    }

    public void OpenEditAssetDialog(AssetRowViewModel asset)
    {
        SelectedAsset = asset;
        AssetTicker = asset.Ticker;
        AssetName = asset.Name;
        AssetType = asset.Type;
        AssetCurrency = asset.Currency;
        AssetExchange = string.Empty;
        AssetIsin = string.Empty;
        AssetTags = string.Join(",", asset.Tags);
        AssetNotes = string.Empty;
        AssetQuantity = asset.Quantity;
        AssetAverageBuyPrice = asset.AverageBuyPrice;
        AssetCurrentPrice = asset.CurrentPrice;
        AssetValidation = string.Empty;
        IsEditAssetDialogOpen = true;
    }

    public void CancelEditAssetDialog()
    {
        IsEditAssetDialogOpen = false;
    }

    public async Task SaveAssetChangesAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedPortfolio is null || SelectedAsset is null)
        {
            AssetValidation = "Актив не выбран.";
            return;
        }

        AssetOperationResult result = await _assets.UpdateAsync(new UpdateAssetRequest(
            SelectedPortfolio.Id,
            SelectedAsset.Id,
            AssetTicker,
            AssetName,
            AssetType,
            AssetCurrency,
            AssetExchange,
            AssetIsin,
            ParseTags(AssetTags),
            AssetNotes,
            AssetQuantity,
            AssetAverageBuyPrice,
            AssetCurrentPrice), cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            AssetValidation = result.Message;
            return;
        }

        IsEditAssetDialogOpen = false;
        await ReloadAssetsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ArchiveSelectedAssetAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedPortfolio is null || SelectedAsset is null)
        {
            AssetStatusText = "Актив не выбран.";
            return;
        }

        AssetOperationResult result = await _assets.ArchiveAsync(SelectedPortfolio.Id, SelectedAsset.Id, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            AssetStatusText = result.Message;
            return;
        }

        IsEditAssetDialogOpen = false;
        await ReloadAssetsAsync(cancellationToken).ConfigureAwait(false);
    }

    public string SelectedAssetTitle => SelectedAsset is null ? "Актив не выбран" : $"{SelectedAsset.Name} ({SelectedAsset.Ticker})";

    public string SelectedTransactionTitle => SelectedTransaction is null
        ? "Транзакция не выбрана"
        : $"{SelectedTransaction.TypeLabel} · {SelectedTransaction.GrossAmount:0.##} {SelectedTransaction.Currency}";

    private async Task ReloadAssetsAsync(CancellationToken cancellationToken)
    {
        Assets.Clear();
        if (SelectedPortfolio is null)
        {
            RefreshAssetTable();
            return;
        }

        IReadOnlyList<Asset> items = await _assets.ListActiveAsync(SelectedPortfolio.Id, cancellationToken).ConfigureAwait(false);
        foreach (Asset asset in items)
        {
            decimal value = asset.Quantity * asset.CurrentPrice;
            decimal pnl = asset.Quantity * (asset.CurrentPrice - asset.AverageBuyPrice);
            Assets.Add(new AssetRowViewModel(
                asset.Id,
                asset.Name,
                asset.Ticker,
                asset.Type,
                asset.Currency,
                asset.Quantity,
                asset.AverageBuyPrice,
                asset.CurrentPrice,
                value,
                pnl,
                asset.Tags));
        }

        RefreshAssetTable();
    }

    public void OpenCreateTransactionDialog()
    {
        ResetTransactionForm();
        IsCreateTransactionDialogOpen = true;
    }

    public void CancelCreateTransactionDialog()
    {
        IsCreateTransactionDialogOpen = false;
    }

    public async Task CreateTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedPortfolio is null)
        {
            TransactionValidation = "Сначала выберите портфель.";
            return;
        }

        TransactionOperationResult result = await _transactions.CreateAsync(new CreateTransactionRequest(
            SelectedPortfolio.Id,
            TransactionAssetId,
            TransactionType,
            TransactionDate,
            TransactionQuantity,
            TransactionPrice,
            TransactionGrossAmount,
            TransactionFeeAmount,
            TransactionTaxAmount,
            TransactionCurrency,
            TransactionBroker,
            TransactionExternalId,
            TransactionNotes), cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            TransactionValidation = result.Message;
            return;
        }

        IsCreateTransactionDialogOpen = false;
        await ReloadTransactionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public void OpenEditTransactionDialog(TransactionRowViewModel row)
    {
        SelectedTransaction = row;
        TransactionAssetId = row.AssetId;
        TransactionType = row.Type;
        TransactionDate = row.TradeDate;
        TransactionQuantity = row.Quantity;
        TransactionPrice = row.Price;
        TransactionGrossAmount = row.GrossAmount;
        TransactionFeeAmount = row.FeeAmount;
        TransactionTaxAmount = row.TaxAmount;
        TransactionCurrency = row.Currency;
        TransactionBroker = row.Broker ?? string.Empty;
        TransactionExternalId = string.Empty;
        TransactionNotes = string.Empty;
        TransactionValidation = string.Empty;
        IsEditTransactionDialogOpen = true;
    }

    public void CancelEditTransactionDialog()
    {
        IsEditTransactionDialogOpen = false;
    }

    public async Task SaveTransactionChangesAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedPortfolio is null || SelectedTransaction is null)
        {
            TransactionValidation = "Транзакция не выбрана.";
            return;
        }

        TransactionOperationResult result = await _transactions.UpdateAsync(new UpdateTransactionRequest(
            SelectedPortfolio.Id,
            SelectedTransaction.Id,
            TransactionAssetId,
            TransactionType,
            TransactionDate,
            TransactionQuantity,
            TransactionPrice,
            TransactionGrossAmount,
            TransactionFeeAmount,
            TransactionTaxAmount,
            TransactionCurrency,
            TransactionBroker,
            TransactionExternalId,
            TransactionNotes), cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            TransactionValidation = result.Message;
            return;
        }

        IsEditTransactionDialogOpen = false;
        await ReloadTransactionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ArchiveSelectedTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedPortfolio is null || SelectedTransaction is null)
        {
            TransactionStatusText = "Транзакция не выбрана.";
            return;
        }

        TransactionOperationResult result = await _transactions.ArchiveAsync(SelectedPortfolio.Id, SelectedTransaction.Id, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            TransactionStatusText = result.Message;
            return;
        }

        IsEditTransactionDialogOpen = false;
        await ReloadTransactionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public void StartCreateTransactionForAsset(AssetRowViewModel asset)
    {
        SelectedAsset = asset;
        OpenCreateTransactionDialog();
        TransactionAssetId = asset.Id;
        if (TransactionType is not TransactionType.Buy and not TransactionType.Sell)
        {
            TransactionType = TransactionType.Buy;
        }
    }

    private void RefreshAssetTable()
    {
        IEnumerable<AssetRowViewModel> query = Assets;
        if (!string.IsNullOrWhiteSpace(AssetSearchQuery))
        {
            string term = AssetSearchQuery.Trim();
            query = query.Where(item =>
                item.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Ticker.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Tags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        query = AssetSort switch
        {
            "name_asc" => query.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase),
            "pnl_desc" => query.OrderByDescending(static item => item.ProfitLoss),
            _ => query.OrderByDescending(static item => item.Value),
        };

        FilteredAssets.Clear();
        foreach (AssetRowViewModel item in query)
        {
            FilteredAssets.Add(item);
        }

        if (SelectedAsset is not null && FilteredAssets.All(item => item.Id != SelectedAsset.Id))
        {
            SelectedAsset = null;
        }

        AssetStatusText = FilteredAssets.Count == 0 ? "Нет активов для отображения." : $"Активов: {FilteredAssets.Count}";
        OnPropertyChanged(nameof(HasAssets));
        OnPropertyChanged(nameof(IsAssetTableEmpty));
        OnPropertyChanged(nameof(SelectedAssetTitle));
    }

    private async Task ReloadTransactionsAsync(CancellationToken cancellationToken)
    {
        Transactions.Clear();
        if (SelectedPortfolio is null)
        {
            RefreshTransactionTable();
            return;
        }

        IReadOnlyList<PortfolioTransaction> items = await _transactions.ListActiveAsync(SelectedPortfolio.Id, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, AssetRowViewModel> assetsById = Assets.ToDictionary(item => item.Id, item => item);
        foreach (PortfolioTransaction item in items)
        {
            AssetRowViewModel? asset = item.AssetId is not null && assetsById.TryGetValue(item.AssetId.Value, out AssetRowViewModel? value) ? value : null;
            Transactions.Add(new TransactionRowViewModel(
                item.Id,
                item.AssetId,
                asset?.Name ?? "Портфель",
                asset?.Ticker ?? "—",
                item.Type,
                item.TradeDate,
                item.Quantity,
                item.Price,
                item.GrossAmount,
                item.FeeAmount,
                item.TaxAmount,
                item.Currency,
                item.Broker));
        }

        RefreshTransactionTable();
    }

    private void RefreshTransactionTable()
    {
        IEnumerable<TransactionRowViewModel> query = Transactions;

        if (IsAssetDetailsPage && SelectedAsset is not null)
        {
            query = query.Where(item => item.AssetId == SelectedAsset.Id);
        }

        if (!string.IsNullOrWhiteSpace(TransactionSearchQuery))
        {
            string term = TransactionSearchQuery.Trim();
            query = query.Where(item =>
                item.AssetName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Ticker.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.TypeLabel.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (item.Broker?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        query = TransactionSort switch
        {
            "amount_desc" => query.OrderByDescending(static item => item.GrossAmount),
            "type_asc" => query.OrderBy(static item => item.TypeLabel, StringComparer.OrdinalIgnoreCase),
            "asset_asc" => query.OrderBy(static item => item.AssetName, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderByDescending(static item => item.TradeDate),
        };

        FilteredTransactions.Clear();
        foreach (TransactionRowViewModel item in query)
        {
            FilteredTransactions.Add(item);
        }

        if (SelectedTransaction is not null && FilteredTransactions.All(item => item.Id != SelectedTransaction.Id))
        {
            SelectedTransaction = null;
        }

        TransactionStatusText = FilteredTransactions.Count == 0
            ? "Нет транзакций для отображения."
            : $"Транзакций: {FilteredTransactions.Count}";

        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(IsTransactionTableEmpty));
        OnPropertyChanged(nameof(SelectedTransactionTitle));
    }

    private static IReadOnlyList<string> ParseTags(string raw)
    {
        return raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private void ResetAssetForm()
    {
        AssetTicker = string.Empty;
        AssetName = string.Empty;
        AssetType = AssetType.Stock;
        AssetCurrency = SelectedPortfolio?.Currency ?? "USD";
        AssetExchange = string.Empty;
        AssetIsin = string.Empty;
        AssetTags = string.Empty;
        AssetNotes = string.Empty;
        AssetQuantity = 0;
        AssetAverageBuyPrice = 0;
        AssetCurrentPrice = 0;
        AssetValidation = string.Empty;
    }

    private void ResetTransactionForm()
    {
        TransactionAssetId = SelectedAsset?.Id;
        TransactionType = TransactionType.Buy;
        TransactionDate = DateTimeOffset.UtcNow;
        TransactionQuantity = 0;
        TransactionPrice = 0;
        TransactionGrossAmount = 0;
        TransactionFeeAmount = 0;
        TransactionTaxAmount = 0;
        TransactionCurrency = SelectedPortfolio?.Currency ?? "USD";
        TransactionBroker = string.Empty;
        TransactionExternalId = string.Empty;
        TransactionNotes = string.Empty;
        TransactionValidation = string.Empty;
    }
}
