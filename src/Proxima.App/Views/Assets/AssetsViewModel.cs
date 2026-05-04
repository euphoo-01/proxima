using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.Navigation;
using Proxima.App.Shell;
using Proxima.App.ViewModels;
using Proxima.App.Views.Import;
using Proxima.Application.Assets;
using Proxima.Domain.Assets;

namespace Proxima.App.Views.Assets;

public sealed class AssetsViewModel : ViewModelBase
{
    private readonly IAppNavigationService _navigation;
    private readonly IAssetService _assetService;
    private readonly IShellState _shellState;
    private readonly DelegateCommand _openImportDialogCommand;
    private readonly DelegateCommand _openAssetDetailsCommand;
    private bool _isLoading;
    private bool _hasError;
    private string _errorText = string.Empty;

    public AssetsViewModel(
        IAppNavigationService navigation,
        ImportDialogViewModel importDialog,
        IAssetService assetService,
        IShellState shellState,
        IRuntimeDataInvalidation dataInvalidation)
    {
        _navigation = navigation;
        _assetService = assetService;
        _shellState = shellState;
        ImportDialog = importDialog;
        ImportDialog.RequestClose += HandleImportDialogClose;
        ImportDialog.RequestOpenManualImport += HandleImportDialogOpenManualImport;

        _openImportDialogCommand = new DelegateCommand(_ => OpenImportDialog());
        _openAssetDetailsCommand = new DelegateCommand(OpenAssetDetails);
        Assets = [];

        _shellState.PortfolioChanged += (_, _) => _ = LoadAsync();
        dataInvalidation.DataInvalidated += (_, _) => _ = LoadAsync();
        _ = LoadAsync();
    }

    public ICommand OpenImportDialogCommand => _openImportDialogCommand;
    public ICommand OpenAssetDetailsCommand => _openAssetDetailsCommand;

    public ImportDialogViewModel ImportDialog { get; }
    public ObservableCollection<AssetListItemViewModel> Assets { get; }

    public string PageTitle => "Все активы";

    public string PageDescription => "Управляйте активами и загружайте операции из отчётов брокера.";

    public bool IsImportDialogOpen => ImportDialog.IsOpen;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasContent));
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasContent));
            }
        }
    }

    public bool IsEmpty => !IsLoading && !HasError && Assets.Count == 0;

    public bool HasContent => !IsLoading && !HasError;

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorText = string.Empty;

        try
        {
            IReadOnlyList<Asset> assets = await _assetService
                .ListActiveAsync(_shellState.CurrentPortfolioId, CancellationToken.None)
                .ConfigureAwait(true);

            Assets.Clear();
            foreach (Asset asset in assets)
            {
                Assets.Add(new AssetListItemViewModel(
                    asset.Id,
                    asset.Name,
                    asset.Ticker,
                    asset.Type,
                    asset.Currency,
                    asset.Quantity,
                    asset.Quantity * asset.CurrentPrice));
            }

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasContent));
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenImportDialog()
    {
        ImportDialog.Open();
        OnPropertyChanged(nameof(IsImportDialogOpen));
    }

    private void HandleImportDialogClose(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsImportDialogOpen));
    }

    private void HandleImportDialogOpenManualImport(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsImportDialogOpen));
        _navigation.Navigate(AppRoutes.ManualImport);
    }

    private void OpenAssetDetails(object? parameter)
    {
        if (parameter is not AssetListItemViewModel item)
        {
            return;
        }

        _navigation.Navigate(
            AppRoutes.AssetDetails,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["assetId"] = item.Id.ToString()
            },
            titleOverride: item.DisplayName,
            breadcrumbOverride: $"Все активы / {item.DisplayName}");
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

public sealed record AssetListItemViewModel(
    Guid Id,
    string Name,
    string Ticker,
    AssetType Type,
    string Currency,
    decimal Quantity,
    decimal Value)
{
    public string DisplayName => $"{Name} ({Ticker})";
    public string TypeLabel => Type.ToString();
    public string QuantityText => $"{Quantity:N4}";
    public string ValueText => $"{Value:N2} {Currency}";
}
