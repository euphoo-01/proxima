using Proxima.Domain.Auth;

namespace Proxima.Application.Auth;

public sealed record CreateProfileRequest(
    string DisplayName,
    string Login,
    string Password,
    string PasswordConfirmation,
    UserRole Role);
