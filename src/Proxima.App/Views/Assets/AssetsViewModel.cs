using System.Windows.Input;
using Proxima.App.Navigation;
using Proxima.App.ViewModels;
using Proxima.App.Views.Import;

namespace Proxima.App.Views.Assets;

public sealed class AssetsViewModel : ViewModelBase
{
    private readonly IAppNavigationService _navigation;

    public AssetsViewModel(IAppNavigationService navigation, ImportDialogViewModel importDialog)
    {
        _navigation = navigation;
        ImportDialog = importDialog;
        ImportDialog.RequestClose += HandleImportDialogClose;
        ImportDialog.RequestOpenManualImport += HandleImportDialogOpenManualImport;

        _openImportDialogCommand = new DelegateCommand(_ => OpenImportDialog());
    }

    private readonly DelegateCommand _openImportDialogCommand;

    public ICommand OpenImportDialogCommand => _openImportDialogCommand;

    public ImportDialogViewModel ImportDialog { get; }

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

    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        private readonly Action<object?> _execute = execute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
