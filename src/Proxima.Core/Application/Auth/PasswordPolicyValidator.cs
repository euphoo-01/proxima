namespace Proxima.Core.Application.Auth;

public sealed class PasswordPolicyValidator
{
    public PasswordValidationResult Validate(string password, string confirmation)
    {
        List<string> errors = [];

        if (password.Length < 8)
        {
            errors.Add("Пароль должен быть не короче 8 символов.");
        }

        if (!password.Any(char.IsLetter))
        {
            errors.Add("Пароль должен содержать хотя бы одну букву.");
        }

        if (!password.Any(character => char.IsDigit(character) || char.IsPunctuation(character) || char.IsSymbol(character)))
        {
            errors.Add("Пароль должен содержать хотя бы одну цифру или символ.");
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            errors.Add("Подтверждение пароля не совпадает.");
        }

        return errors.Count == 0
            ? PasswordValidationResult.Valid
            : new PasswordValidationResult(false, errors);
    }
}
