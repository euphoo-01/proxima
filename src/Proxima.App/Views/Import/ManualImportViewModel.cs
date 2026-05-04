using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.ViewModels;
using Proxima.Domain.Transactions;
using Proxima.Importing;

namespace Proxima.App.Views.Import;

public sealed class ManualImportViewModel : ViewModelBase
{
    private readonly DelegateCommand _addRowCommand;
    private readonly DelegateCommand _removeRowCommand;
    private readonly DelegateCommand _saveCommand;

    private string _statusMessage = "Добавьте или проверьте строки перед импортом.";
    private bool _hasWarnings;

    public ManualImportViewModel()
    {
        Rows = [];
        OperationTypes = Enum.GetNames<TransactionType>();

        _addRowCommand = new DelegateCommand(_ => AddRow());
        _removeRowCommand = new DelegateCommand(row => RemoveRow(row as ManualTransactionRowViewModel));
        _saveCommand = new DelegateCommand(_ => SaveImport());

        AddRow();
    }

    public ObservableCollection<ManualTransactionRowViewModel> Rows { get; }

    public IReadOnlyList<string> OperationTypes { get; }

    public ICommand AddRowCommand => _addRowCommand;

    public ICommand RemoveRowCommand => _removeRowCommand;

    public ICommand SaveCommand => _saveCommand;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasWarnings
    {
        get => _hasWarnings;
        private set => SetProperty(ref _hasWarnings, value);
    }

    public int SuspiciousCount => Rows.Count(static row => row.IsSuspicious);

    public int TotalCount => Rows.Count;

    public void LoadFromPreview(ImportPreview preview)
    {
        Rows.Clear();
        foreach (ImportedTransactionRow row in preview.Rows)
        {
            Rows.Add(new ManualTransactionRowViewModel
            {
                Date = row.TradeDate.ToString("yyyy-MM-dd"),
                TickerOrName = string.IsNullOrWhiteSpace(row.AssetTicker) ? row.AssetName : row.AssetTicker,
                OperationType = row.TransactionType.ToString(),
                Quantity = row.Quantity.ToString("0.####"),
                Price = row.Price.ToString("0.####"),
                Commission = row.FeeAmount.ToString("0.####"),
                Currency = string.IsNullOrWhiteSpace(row.Currency) ? "USD" : row.Currency,
                TagOrCategory = row.Tag ?? string.Empty,
                IsSuspicious = row.Status != ImportRowStatus.Valid,
                SuspiciousReason = row.StatusReason ?? string.Empty,
            });
        }

        if (Rows.Count == 0)
        {
            AddRow();
        }

        RecalculateWarnings();
        StatusMessage = preview.Succeeded
            ? $"Загружено строк: {Rows.Count}. Проверьте и сохраните."
            : "Автоматический импорт не удался. Заполните данные вручную.";
    }

    private void AddRow()
    {
        Rows.Add(new ManualTransactionRowViewModel
        {
            Date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
            OperationType = TransactionType.Buy.ToString(),
            Currency = "USD",
        });

        RecalculateWarnings();
    }

    private void RemoveRow(ManualTransactionRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        Rows.Remove(row);
        if (Rows.Count == 0)
        {
            AddRow();
        }

        RecalculateWarnings();
    }

    private void SaveImport()
    {
        RecalculateWarnings();
        StatusMessage = HasWarnings
            ? "Импорт сохранён с предупреждениями. Подозрительные строки помечены для проверки."
            : "Импорт сохранён.";
    }

    private void RecalculateWarnings()
    {
        foreach (ManualTransactionRowViewModel row in Rows)
        {
            row.RecalculateSuspicion();
        }

        HasWarnings = Rows.Any(static row => row.IsSuspicious);
        OnPropertyChanged(nameof(SuspiciousCount));
        OnPropertyChanged(nameof(TotalCount));
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

public sealed class ManualTransactionRowViewModel : ViewModelBase
{
    private string _date = string.Empty;
    private string _tickerOrName = string.Empty;
    private string _operationType = TransactionType.Buy.ToString();
    private string _quantity = string.Empty;
    private string _price = string.Empty;
    private string _commission = string.Empty;
    private string _currency = "USD";
    private string _tagOrCategory = string.Empty;
    private bool _isSuspicious;
    private string _suspiciousReason = string.Empty;

    public string Date
    {
        get => _date;
        set => SetProperty(ref _date, value);
    }

    public string TickerOrName
    {
        get => _tickerOrName;
        set => SetProperty(ref _tickerOrName, value);
    }

    public string OperationType
    {
        get => _operationType;
        set => SetProperty(ref _operationType, value);
    }

    public string Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    public string Price
    {
        get => _price;
        set => SetProperty(ref _price, value);
    }

    public string Commission
    {
        get => _commission;
        set => SetProperty(ref _commission, value);
    }

    public string Currency
    {
        get => _currency;
        set => SetProperty(ref _currency, value);
    }

    public string TagOrCategory
    {
        get => _tagOrCategory;
        set => SetProperty(ref _tagOrCategory, value);
    }

    public bool IsSuspicious
    {
        get => _isSuspicious;
        set => SetProperty(ref _isSuspicious, value);
    }

    public string SuspiciousReason
    {
        get => _suspiciousReason;
        set => SetProperty(ref _suspiciousReason, value);
    }

    public void RecalculateSuspicion()
    {
        bool invalidDate = !DateTimeOffset.TryParse(Date, out _);
        bool invalidQty = !decimal.TryParse(Quantity, out decimal qty) || qty <= 0;
        bool invalidPrice = !decimal.TryParse(Price, out decimal price) || price <= 0;
        bool missingAsset = string.IsNullOrWhiteSpace(TickerOrName);

        IsSuspicious = invalidDate || invalidQty || invalidPrice || missingAsset;
        SuspiciousReason = IsSuspicious
            ? "Проверьте дату/тикер/количество/цену"
            : string.Empty;
    }
}
