using Proxima.Domain.Auth;

namespace Proxima.Application.Settings;

public sealed class SettingsService(IUserSettingsRepository repository) : ISettingsService
{
    public async Task<UserSettings> EnsureAsync(CreateDefaultSettingsRequest request, CancellationToken cancellationToken = default)
    {
        UserSettings? existing = await repository.FindByOwnerAsync(request.OwnerUserId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        UserSettings settings = new(
            request.OwnerUserId,
            request.DisplayName.Trim(),
            request.Role,
            request.Login.Trim().ToLowerInvariant(),
            NormalizeCurrency(request.PreferredCurrency),
            AppLanguage.RU,
            1m,
            QuoteProviderKind.Finnhub,
            15,
            string.Empty,
            CurrencyProviderKind.Mock,
            false,
            null);

        await repository.UpsertAsync(settings, cancellationToken).ConfigureAwait(false);
        return settings;
    }

    public Task<UserSettings?> GetAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        return repository.FindByOwnerAsync(ownerUserId, cancellationToken);
    }

    public async Task<SettingsOperationResult> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken = default)
    {
        string? validation = Validate(request);
        if (!string.IsNullOrWhiteSpace(validation))
        {
            return SettingsOperationResult.Failure(validation);
        }

        UserSettings? current = await repository.FindByOwnerAsync(request.OwnerUserId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return SettingsOperationResult.Failure("Настройки не инициализированы.");
        }

        string protectedKey = current.FinnhubApiKeyProtected;
        if (request.FinnhubApiKeyRaw is not null)
        {
            protectedKey = Protect(request.FinnhubApiKeyRaw);
        }

        UserSettings updated = current with
        {
            DisplayName = request.DisplayName.Trim(),
            Role = request.Role,
            PreferredCurrency = NormalizeCurrency(request.PreferredCurrency),
            Language = request.Language,
            UiScale = request.UiScale,
            QuoteProvider = request.QuoteProvider,
            QuoteRefreshMinutes = request.QuoteRefreshMinutes,
            FinnhubApiKeyProtected = protectedKey,
            CurrencyProvider = request.CurrencyProvider,
            SyncEnabled = request.SyncEnabled,
        };

        await repository.UpsertAsync(updated, cancellationToken).ConfigureAwait(false);
        return SettingsOperationResult.Success(updated);
    }

    private static string? Validate(UpdateSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return "Display name обязателен.";
        }

        if (request.UiScale is < 0.8m or > 1.5m)
        {
            return "UI scale должен быть в диапазоне 0.8..1.5.";
        }

        if (request.QuoteRefreshMinutes is < 1 or > 240)
        {
            return "Интервал обновления котировок должен быть 1..240 минут.";
        }

        string currency = NormalizeCurrency(request.PreferredCurrency);
        if (currency is not ("USD" or "BYN" or "EUR" or "RUB"))
        {
            return "Недопустимая базовая валюта.";
        }

        if (!Enum.IsDefined(request.Role))
        {
            return "Недопустимая роль профиля.";
        }

        return null;
    }

    private static string NormalizeCurrency(string currency) => string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();

    private static string Protect(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw.Trim()));
    }
}
