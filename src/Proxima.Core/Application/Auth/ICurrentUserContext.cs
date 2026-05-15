using Proxima.Core.Domain.Auth;

namespace Proxima.Core.Application.Auth;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string Login { get; }

    string DisplayName { get; }

    UserRole Role { get; }
}
