using System.Collections.ObjectModel;
using Proxima.App.ViewModels;

namespace Proxima.App.Views.Support;

public sealed class SupportViewModel : ViewModelBase
{
    public SupportViewModel()
    {
        Items =
        [
            new SupportFaqItem("Где хранятся мои данные?", "Данные приложения хранятся локально. Финансовые данные не отправляются на сторонние серверы без вашего действия."),
            new SupportFaqItem("Как добавить активы?", "Откройте раздел «Все активы» и используйте импорт CSV/PDF или ручной ввод транзакций."),
            new SupportFaqItem("Почему графики могут быть пустыми?", "Графики строятся на основе активов, транзакций и сохраненных котировок. Если портфель пуст, приложение покажет пустое состояние."),
            new SupportFaqItem("Как работает оценка риска?", "Оценка риска рассчитывается по концентрации крупнейшей категории, доле криптовалют, исторической просадке и количеству активов."),
            new SupportFaqItem("Можно ли работать офлайн?", "Да. Приложение использует локальные данные и последние сохраненные котировки. Актуализация цен требует доступа к интернету."),
            new SupportFaqItem("Как создать несколько портфелей?", "Создание нескольких портфелей доступно роли «Финансовый аналитик». Для частного инвестора используется основной портфель.")
        ];
    }

    public ObservableCollection<SupportFaqItem> Items { get; }

    public string ContactEmail => "stas.lavshuk@yandex.com";

    public string ProductInfo => "Proxima создана Лавшуком Станиславом Александровичем как MVP системы финансового анализа и управления частным капиталом.";
}

public sealed record SupportFaqItem(string Question, string Answer);
