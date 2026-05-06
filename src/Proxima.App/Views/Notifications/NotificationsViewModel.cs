using System.Collections.ObjectModel;
using Proxima.App.ViewModels;

namespace Proxima.App.Views.Notifications;

public sealed class NotificationsViewModel : ViewModelBase
{
    public NotificationsViewModel()
    {
        Items =
        [
            new NotificationItem("Котировки", "При наличии интернета приложение обновляет цены через настроенный провайдер котировок.", "Информация"),
            new NotificationItem("Импорт", "После импорта транзакций дашборд автоматически пересчитывает баланс, риск и распределение.", "Подсказка"),
            new NotificationItem("Безопасность", "Финансовые данные остаются локально на устройстве.", "Важно")
        ];
    }

    public ObservableCollection<NotificationItem> Items { get; }
}

public sealed record NotificationItem(string Title, string Message, string Kind);
