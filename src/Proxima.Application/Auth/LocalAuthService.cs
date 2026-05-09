using Proxima.Domain.Auth;

namespace Proxima.Application.Auth;

public sealed class LocalAuthService(
    ILocalUserRepository users,
    IPasswordHasher passwordHasher,
    PasswordPolicyValidator passwordPolicy)
    : ILocalAuthService
{
    private const string GenericAuthError = "Не удалось разблокировать Proxima. Проверьте логин и пароль.";

    public async Task<bool> NeedsFirstRunSetupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return !await users.HasAnyProfileAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    public async Task<AuthResult> CreateProfileAsync(CreateProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.Login))
        {
            return AuthResult.Failure(AuthFailureReason.InvalidInput, "Введите имя и логин.");
        }

        PasswordValidationResult validation = passwordPolicy.Validate(request.Password, request.PasswordConfirmation);
        if (!validation.IsValid)
        {
            return AuthResult.Failure(AuthFailureReason.InvalidInput, string.Join(Environment.NewLine, validation.Errors));
        }

        try
        {
            if (await users.HasAnyProfileAsync(cancellationToken).ConfigureAwait(false))
            {
                return AuthResult.Failure(AuthFailureReason.ProfileAlreadyExists, "Локальный профиль уже создан.");
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            LocalUserProfile profile = new(
                Guid.NewGuid(),
                request.DisplayName.Trim(),
                NormalizeLogin(request.Login),
                request.Role,
                passwordHasher.Hash(request.Password),
                now,
                now,
                FailedUnlockAttempts: 0);

            await users.AddAsync(profile, cancellationToken).ConfigureAwait(false);
            return AuthResult.Success(profile);
        }
        catch (IOException)
        {
            return AuthResult.Failure(AuthFailureReason.StorageUnavailable, "Локальное хранилище профиля недоступно.");
        }
        catch (UnauthorizedAccessException)
        {
            return AuthResult.Failure(AuthFailureReason.StorageUnavailable, "Нет доступа к локальному хранилищу профиля.");
        }
    }

    public async Task<AuthResult> UnlockAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
        {
            return AuthResult.Failure(AuthFailureReason.GenericAuthenticationFailed, GenericAuthError);
        }

        try
        {
            LocalUserProfile? profile = await users.FindByLoginAsync(NormalizeLogin(login), cancellationToken).ConfigureAwait(false);
            if (profile is null || !passwordHasher.Verify(password, profile.Credential))
            {
                if (profile is not null)
                {
                    await users.UpdateAsync(profile with
                    {
                        FailedUnlockAttempts = profile.FailedUnlockAttempts + 1,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    }, cancellationToken).ConfigureAwait(false);
                }

                return AuthResult.Failure(AuthFailureReason.GenericAuthenticationFailed, GenericAuthError);
            }

            LocalUserProfile unlocked = profile with
            {
                FailedUnlockAttempts = 0,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await users.UpdateAsync(unlocked, cancellationToken).ConfigureAwait(false);
            return AuthResult.Success(unlocked);
        }
        catch (IOException)
        {
            return AuthResult.Failure(AuthFailureReason.StorageUnavailable, "Локальное хранилище профиля недоступно.");
        }
        catch (UnauthorizedAccessException)
        {
            return AuthResult.Failure(AuthFailureReason.StorageUnavailable, "Нет доступа к локальному хранилищу профиля.");
        }
    }

    public async Task DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty)
        {
            return;
        }

        await users.DeleteAsync(profileId, cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeLogin(string login) => login.Trim().ToLowerInvariant();
}
