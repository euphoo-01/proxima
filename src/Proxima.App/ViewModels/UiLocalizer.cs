using Proxima.Core.Application.Settings;

namespace Proxima.App.ViewModels;

internal static class UiLocalizer
{
    private static readonly Dictionary<string, string> Ru = new(StringComparer.Ordinal)
    {
        ["nav.dashboard"] = "Дешборд",
        ["nav.assets"] = "Все активы",
        ["nav.taxes"] = "Налоги",
        ["nav.goals"] = "Цели",
        ["nav.settings"] = "Настройки",
        ["action.back"] = "Назад",
        ["action.createPortfolio"] = "Создать портфель",
        ["action.manage"] = "Управлять",
        ["action.importAssets"] = "Импортировать активы",
        ["action.manualInput"] = "Ввести вручную",
        ["page.dashboard.title"] = "Дешборд",
        ["page.dashboard.desc"] = "Ключевые показатели портфеля и последние транзакции.",
        ["page.assets.title"] = "Все активы",
        ["page.assets.desc"] = "Список активов, фильтры и быстрые действия.",
        ["page.taxes.title"] = "Налоги",
        ["page.taxes.desc"] = "Черновик налогового расчёта для РБ.",
        ["page.goals.title"] = "Цели",
        ["page.goals.desc"] = "Финансовые цели и прогноз накоплений.",
        ["page.settings.title"] = "Настройки",
        ["page.settings.desc"] = "Параметры приложения и профиля.",
    };

    private static readonly Dictionary<string, string> En = new(StringComparer.Ordinal)
    {
        ["nav.dashboard"] = "Dashboard",
        ["nav.assets"] = "Assets",
        ["nav.taxes"] = "Taxes",
        ["nav.goals"] = "Goals",
        ["nav.settings"] = "Settings",
        ["action.back"] = "Back",
        ["action.createPortfolio"] = "Create Portfolio",
        ["action.manage"] = "Manage",
        ["action.importAssets"] = "Import Assets",
        ["action.manualInput"] = "Manual Input",
        ["page.dashboard.title"] = "Dashboard",
        ["page.dashboard.desc"] = "Portfolio KPIs and latest transactions.",
        ["page.assets.title"] = "Assets",
        ["page.assets.desc"] = "Asset list, filters and quick actions.",
        ["page.taxes.title"] = "Taxes",
        ["page.taxes.desc"] = "Belarus tax draft calculation.",
        ["page.goals.title"] = "Goals",
        ["page.goals.desc"] = "Financial goals and accumulation forecast.",
        ["page.settings.title"] = "Settings",
        ["page.settings.desc"] = "App and profile preferences.",
    };

    public static string Get(string key, AppLanguage language)
    {
        Dictionary<string, string> source = language == AppLanguage.EN ? En : Ru;
        return source.TryGetValue(key, out string? value) ? value : key;
    }
}
