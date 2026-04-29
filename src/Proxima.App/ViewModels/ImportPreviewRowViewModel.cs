using Proxima.Importing;

namespace Proxima.App.ViewModels;

public sealed class ImportPreviewRowViewModel : ViewModelBase
{
    private bool _isSelectedForCommit;

    public ImportPreviewRowViewModel(ImportedTransactionRow row)
    {
        Source = row;
        _isSelectedForCommit = row.Status is ImportRowStatus.Valid or ImportRowStatus.Suspicious;
    }

    public ImportedTransactionRow Source { get; }

    public bool IsSelectedForCommit
    {
        get => _isSelectedForCommit;
        set => SetProperty(ref _isSelectedForCommit, value);
    }

    public string StatusLabel => Source.Status.ToString();
}
