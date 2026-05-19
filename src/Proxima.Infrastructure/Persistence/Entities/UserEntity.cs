namespace Proxima.Infrastructure.Persistence;

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Location { get; set; } = "Минск, Беларусь";
    public string LegalProfile { get; set; } = "PhysicalPerson";
    public int FailedUnlockAttempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
