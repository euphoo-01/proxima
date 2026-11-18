using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.Common.Commands;
using Proxima.App.ViewModels;
using Proxima.Core.Application.Database;

namespace Proxima.App.Views.Database;

public sealed class DatabaseCatalogViewModel(IDatabaseCatalog catalog) : ViewModelBase
{
    private string _status = "Проверка соединения ещё не выполнена.";
    private string _server = "";
    private bool _isBusy;
    private AsyncCommand? _refreshCommand;

    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Server { get => _server; private set => SetProperty(ref _server, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public ObservableCollection<CatalogCurrency> Currencies { get; } = [];
    public ObservableCollection<CatalogInstrument> Instruments { get; } = [];
    public ICommand RefreshCommand => _refreshCommand ??= new AsyncCommand(RefreshAsync);

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = "Подключаемся к Oracle и загружаем справочники…";
        Server = "";
        Currencies.Clear();
        Instruments.Clear();
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            DatabaseCatalogSnapshot snapshot = await catalog.ReadAsync(timeout.Token);
            Server = $"База: {snapshot.Server.DatabaseName}   •   Пользователь: {snapshot.Server.SessionUser}\nВремя сервера: {snapshot.Server.ServerTime}";
            foreach (CatalogCurrency currency in snapshot.Currencies) Currencies.Add(currency);
            foreach (CatalogInstrument instrument in snapshot.Instruments) Instruments.Add(instrument);
            Status = $"Соединение установлено. Загружено валют: {Currencies.Count}, инструментов: {Instruments.Count}.";
        }
        catch (DatabaseCatalogNotConfiguredException)
        {
            Status = "Подключение к Oracle не настроено. Обратитесь к администратору приложения.";
        }
        catch (OperationCanceledException)
        {
            Status = "Время ожидания истекло. Проверьте доступность Oracle и повторите проверку.";
        }
        catch (Exception)
        {
            // в сообщении драйвера могут оказаться реквизиты подключения
            Status = "Не удалось загрузить данные. Проверьте доступность базы Oracle и настройки доступа, затем повторите проверку.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
