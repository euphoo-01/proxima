namespace Proxima.Core.Domain.Auth;

public enum UserRole
{
    PrivateInvestor = 0,
    FinancialAnalyst = 1,
}

public static class UserRoleExtensions
{
    public static string ToDisplayName(this UserRole role)
    {
        return role switch
        {
            UserRole.FinancialAnalyst => "Финансовый консультант",
            _ => "Частный инвестор",
        };
    }

    public static string ToShortDisplayName(this UserRole role)
    {
        return role switch
        {
            UserRole.FinancialAnalyst => "Финансовый консультант",
            _ => "Частный инвестор",
        };
    }
}
