using System.Windows.Input;
using Proxima.App.Navigation;
using Proxima.App.ViewModels;
using Proxima.App.Views.Import;
using Proxima.Domain.Assets;

namespace Proxima.App.Views.Assets;

public sealed class AssetsViewModel : ViewModelBase
{
    private readonly IAppNavigationService _navigation;
    private readonly DelegateCommand _openAssetDetailsCommand;
    private readonly IReadOnlyList<AssetListItemViewModel> _assets;

    public AssetsViewModel(IAppNavigationService navigation, ImportDialogViewModel importDialog)
    {
        _navigation = navigation;
        ImportDialog = importDialog;
        ImportDialog.RequestClose += HandleImportDialogClose;
        ImportDialog.RequestOpenManualImport += HandleImportDialogOpenManualImport;

        _openImportDialogCommand = new DelegateCommand(_ => OpenImportDialog());
        _openAssetDetailsCommand = new DelegateCommand(OpenAssetDetails);
        _assets =
        [
            new AssetListItemViewModel(Guid.Parse("0a896663-ec39-4ebc-9b28-bf537f8f2fe0"), "Apple Inc.", "AAPL", AssetType.Stock, "USD", 12m, 2223.60m),
            new AssetListItemViewModel(Guid.Parse("f3b4b129-5478-4965-923f-03ef74ca7c07"), "Microsoft", "MSFT", AssetType.Stock, "USD", 9m, 3791.25m),
            new AssetListItemViewModel(Guid.Parse("3ab7f07a-8c4b-44ec-9025-f4d0983e0fbf"), "Bitcoin", "BTC", AssetType.Crypto, "USD", 0.42m, 28784.12m)
        ];
    }

    private readonly DelegateCommand _openImportDialogCommand;

    public ICommand OpenImportDialogCommand => _openImportDialogCommand;
    public ICommand OpenAssetDetailsCommand => _openAssetDetailsCommand;

    public ImportDialogViewModel ImportDialog { get; }
    public IReadOnlyList<AssetListItemViewModel> Assets => _assets;

    public string PageTitle => "Все активы";

    public string PageDescription => "Управляйте активами и загружайте операции из отчётов брокера.";

    public bool IsImportDialogOpen => ImportDialog.IsOpen;

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
