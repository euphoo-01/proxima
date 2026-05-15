using Proxima.Core.Domain.Auth;

namespace Proxima.Core.Application.Auth;

public sealed record CreateProfileRequest(
    string DisplayName,
    string Login,
    string Password,
    string PasswordConfirmation,
    UserRole Role);
