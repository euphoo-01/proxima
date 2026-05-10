using Proxima.Application.Auth;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Auth;

public static class ProximaAuthComposition
{
    public static ILocalAuthService CreateLocalAuthService(
        IProximaUnitOfWorkFactory uowFactory,
        IProximaUnitOfWorkAccessor uowAccessor)
    {
        return new LocalAuthService(
            new PostgresLocalUserRepository(uowFactory, uowAccessor),
            new Pbkdf2PasswordHasher(),
            new PasswordPolicyValidator());
    }
}
