using Proxima.Domain.Auth;

namespace Proxima.Application.Auth;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string Login { get; }

    string DisplayName { get; }

    UserRole Role { get; }
}
