using Proxima.App.ViewModels;
using Proxima.App.Views.Auth;
using Proxima.Domain.Auth;

namespace Proxima.App.Views.Profile;

public sealed class ProfileViewModel(IRuntimeUserContext userContext) : ViewModelBase
{
    public string DisplayName => string.IsNullOrWhiteSpace(userContext.DisplayName) ? "Пользователь" : userContext.DisplayName;

    public string Login => string.IsNullOrWhiteSpace(userContext.Login) ? "Локальный профиль" : userContext.Login;

    public string RoleDisplayName => userContext.Role == UserRole.FinancialAnalyst ? "Финансовый аналитик" : "Частный инвестор";

    public string UserId => userContext.UserId == Guid.Empty ? "—" : userContext.UserId.ToString();

    public string Initial => DisplayName.Trim()[..1].ToUpperInvariant();
}
