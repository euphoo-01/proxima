using Microsoft.EntityFrameworkCore;
using Proxima.Core.Application.Settings;

namespace Proxima.Infrastructure.Persistence.Repositories;

public sealed class PostgresUserSettingsRepository(IProximaUnitOfWorkFactory uowFactory, IProximaUnitOfWorkAccessor uowAccessor) : IUserSettingsRepository
{
    public async Task<UserSettings?> FindByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        UserSettingsEntity? x = await ctx.UserSettings.AsNoTracking()
            .FirstOrDefaultAsync(i => i.OwnerUserId == ownerUserId, cancellationToken)
            .ConfigureAwait(false);

        return x is null ? null : ToDomain(x);
    }

    public async Task UpsertAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;

        UserSettingsEntity? existing = await ctx.UserSettings
            .FirstOrDefaultAsync(i => i.OwnerUserId == settings.OwnerUserId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            ctx.UserSettings.Add(ToEntity(settings));
        }
        else
        {
            existing.QuoteProvider = settings.QuoteProvider.ToString();
            existing.QuoteApiKey = settings.QuoteApiKey;
            existing.CurrencyProvider = settings.CurrencyProvider.ToString();
        }

        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static UserSettings ToDomain(UserSettingsEntity x)
    {
        return new UserSettings(
            x.OwnerUserId,
            ParseQuoteProvider(x.QuoteProvider),
            x.QuoteApiKey,
            ParseCurrencyProvider(x.CurrencyProvider));
    }

    private static QuoteProviderKind ParseQuoteProvider(string value)
    {
        return Enum.TryParse(value, ignoreCase: true, out QuoteProviderKind parsed)
            && Enum.IsDefined(typeof(QuoteProviderKind), parsed)
            ? parsed
            : QuoteProviderKind.TwelveData;
    }

    private static CurrencyProviderKind ParseCurrencyProvider(string value)
    {
        return Enum.TryParse(value, ignoreCase: true, out CurrencyProviderKind parsed)
            && Enum.IsDefined(typeof(CurrencyProviderKind), parsed)
            ? parsed
            : CurrencyProviderKind.Mock;
    }

    private static UserSettingsEntity ToEntity(UserSettings settings)
    {
        return new UserSettingsEntity
        {
            OwnerUserId = settings.OwnerUserId,
            QuoteProvider = settings.QuoteProvider.ToString(),
            QuoteApiKey = settings.QuoteApiKey,
            CurrencyProvider = settings.CurrencyProvider.ToString(),
        };
    }
}
