using Proxima.Domain.Auth;

namespace Proxima.Application.Auth;

public interface IPasswordHasher
{
    PasswordCredential Hash(string password);

    bool Verify(string password, PasswordCredential credential);
}
