namespace Proxima.Core.Application.Settings;

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
            QuoteProviderKind.TwelveData,
            string.Empty,
            CurrencyProviderKind.Mock);

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
            current = await EnsureAsync(new CreateDefaultSettingsRequest(request.OwnerUserId), cancellationToken).ConfigureAwait(false);
        }

        string quoteApiKey = current.QuoteApiKey;
        if (request.QuoteApiKeyRaw is not null)
        {
            quoteApiKey = Protect(request.QuoteApiKeyRaw);
        }

        UserSettings updated = current with
        {
            QuoteProvider = request.QuoteProvider,
            QuoteApiKey = quoteApiKey,
            CurrencyProvider = request.CurrencyProvider,
        };

        await repository.UpsertAsync(updated, cancellationToken).ConfigureAwait(false);
        return SettingsOperationResult.Success(updated);
    }

    private static string? Validate(UpdateSettingsRequest request)
    {
        if (!Enum.IsDefined(request.QuoteProvider))
        {
            return "Недопустимый провайдер котировок.";
        }

        if (!Enum.IsDefined(request.CurrencyProvider))
        {
            return "Недопустимый провайдер курсов валют.";
        }

        return null;
    }

    private static string Protect(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw.Trim()));
    }
}
