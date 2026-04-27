namespace Proxima.Application.Auth;

public sealed record PasswordValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static PasswordValidationResult Valid { get; } = new(true, []);
}
