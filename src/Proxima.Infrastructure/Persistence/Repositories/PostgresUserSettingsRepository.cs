using Microsoft.EntityFrameworkCore;
using Proxima.Application.Settings;
using Proxima.Domain.Auth;

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
            existing.DisplayName = settings.DisplayName;
            existing.Role = settings.Role.ToString();
            existing.Login = settings.Login;
            existing.PreferredCurrency = settings.PreferredCurrency;
            existing.Language = settings.Language.ToString();
            existing.UiScale = settings.UiScale;
            existing.QuoteProvider = settings.QuoteProvider.ToString();
            existing.QuoteRefreshMinutes = settings.QuoteRefreshMinutes;
            existing.FinnhubApiKeyProtected = settings.FinnhubApiKeyProtected;
            existing.CurrencyProvider = settings.CurrencyProvider.ToString();
            existing.SyncEnabled = settings.SyncEnabled;
            existing.LastSnapshotAt = settings.LastSnapshotAt;
        }

        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static UserSettings ToDomain(UserSettingsEntity x)
    {
        return new UserSettings(
            x.OwnerUserId,
            x.DisplayName,
            Enum.Parse<UserRole>(x.Role, true),
            x.Login,
            x.PreferredCurrency,
            Enum.Parse<AppLanguage>(x.Language, true),
            x.UiScale,
            Enum.Parse<QuoteProviderKind>(x.QuoteProvider, true),
            x.QuoteRefreshMinutes,
            x.FinnhubApiKeyProtected,
            Enum.Parse<CurrencyProviderKind>(x.CurrencyProvider, true),
            x.SyncEnabled,
            x.LastSnapshotAt);
    }

    private static UserSettingsEntity ToEntity(UserSettings settings)
    {
        return new UserSettingsEntity
        {
            OwnerUserId = settings.OwnerUserId,
            DisplayName = settings.DisplayName,
            Role = settings.Role.ToString(),
            Login = settings.Login,
            PreferredCurrency = settings.PreferredCurrency,
            Language = settings.Language.ToString(),
            UiScale = settings.UiScale,
            QuoteProvider = settings.QuoteProvider.ToString(),
            QuoteRefreshMinutes = settings.QuoteRefreshMinutes,
            FinnhubApiKeyProtected = settings.FinnhubApiKeyProtected,
            CurrencyProvider = settings.CurrencyProvider.ToString(),
            SyncEnabled = settings.SyncEnabled,
            LastSnapshotAt = settings.LastSnapshotAt,
        };
    }
}
