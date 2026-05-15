namespace Proxima.Core.Application.Auth;

public enum AuthFailureReason
{
    None = 0,
    InvalidInput,
    ProfileAlreadyExists,
    GenericAuthenticationFailed,
    StorageUnavailable,
}
