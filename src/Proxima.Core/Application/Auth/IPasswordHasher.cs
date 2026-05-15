using Proxima.Core.Domain.Auth;

namespace Proxima.Core.Application.Auth;

public interface IPasswordHasher
{
    PasswordCredential Hash(string password);

    bool Verify(string password, PasswordCredential credential);
}
