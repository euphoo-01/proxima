using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Auth;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Auth;

public static class AuthComposition
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddSingleton<ILocalUserRepository, PostgresLocalUserRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<PasswordPolicyValidator>();
        services.AddSingleton<ILocalAuthService, LocalAuthService>();
        return services;
    }
}
