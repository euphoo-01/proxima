using Proxima.Core.Domain.Auth;

namespace Proxima.Core.Application.Auth;

public sealed class LocalAuthService (
    ILocalUserRepository users,
    IPasswordHasher passwordHasher,
    PasswordPolicyValidator passwordPolicy)
    : ILocalAuthService
{
    private const int MaxFailedUnlockAttempts = 3;
    private static readonly TimeSpan UnlockLockoutDuration = TimeSpan.FromMinutes(3);
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
                FailedUnlockAttempts: 0,
                Location: "Минск, Беларусь",
                LegalProfile: LegalProfileKind.PhysicalPerson);

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
        catch (InvalidOperationException ex) when (ex.Message.Contains("same login", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return AuthResult.Failure(AuthFailureReason.ProfileAlreadyExists, "Пользователь с таким логином уже существует.");
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
            if (profile is null)
            {
                return AuthResult.Failure(AuthFailureReason.GenericAuthenticationFailed, GenericAuthError);
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (IsLockedOut(profile, now, out TimeSpan remaining))
            {
                return AuthResult.Failure(
                    AuthFailureReason.GenericAuthenticationFailed,
                    $"Вход временно заблокирован после 3 неверных попыток. Повторите через {FormatRemaining(remaining)}.");
            }

            if (profile.FailedUnlockAttempts >= MaxFailedUnlockAttempts)
            {
                profile = profile with
                {
                    FailedUnlockAttempts = 0,
                    UpdatedAt = now,
                };

                await users.UpdateAsync(profile, cancellationToken).ConfigureAwait(false);
            }

            if (!passwordHasher.Verify(password, profile.Credential))
            {
                int failedAttempts = Math.Min(profile.FailedUnlockAttempts + 1, MaxFailedUnlockAttempts);
                await users.UpdateAsync(profile with
                {
                    FailedUnlockAttempts = failedAttempts,
                    UpdatedAt = now,
                }, cancellationToken).ConfigureAwait(false);

                if (failedAttempts >= MaxFailedUnlockAttempts)
                {
                    return AuthResult.Failure(
                        AuthFailureReason.GenericAuthenticationFailed,
                        "Вход временно заблокирован после 3 неверных попыток. Повторите через 3 минуты.");
                }

                return AuthResult.Failure(AuthFailureReason.GenericAuthenticationFailed, GenericAuthError);
            }

            LocalUserProfile unlocked = profile with
            {
                FailedUnlockAttempts = 0,
                UpdatedAt = now,
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

    private static bool IsLockedOut(LocalUserProfile profile, DateTimeOffset now, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (profile.FailedUnlockAttempts < MaxFailedUnlockAttempts)
        {
            return false;
        }

        DateTimeOffset lockoutEndsAt = profile.UpdatedAt.Add(UnlockLockoutDuration);
        if (now >= lockoutEndsAt)
        {
            return false;
        }

        remaining = lockoutEndsAt - now;
        return true;
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        int seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
        int minutes = (seconds + 59) / 60;
        return minutes == 1 ? "1 минуту" : $"{minutes} минуты";
    }

    private static string NormalizeLogin(string login) => login.Trim().ToLowerInvariant();
}
