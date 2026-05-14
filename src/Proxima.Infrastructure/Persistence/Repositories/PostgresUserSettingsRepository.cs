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

        await EnsureUserRowAsync(ctx, settings, cancellationToken).ConfigureAwait(false);

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
            existing.TwelveDataApiKeyProtected = settings.TwelveDataApiKeyProtected;
            existing.CurrencyProvider = settings.CurrencyProvider.ToString();
        }

        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureUserRowAsync(ProximaDbContext ctx, UserSettings settings, CancellationToken cancellationToken)
    {
        UserEntity? user = await ctx.Users
            .FirstOrDefaultAsync(x => x.Id == settings.OwnerUserId, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string displayName = NormalizeDisplayName(settings.DisplayName);
        string login = await MakeUniqueLoginAsync(ctx, settings.OwnerUserId, settings.Login, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            ctx.Users.Add(new UserEntity
            {
                Id = settings.OwnerUserId,
                DisplayName = displayName,
                Login = login,
                Role = settings.Role.ToString(),
                CreatedAt = now,
                UpdatedAt = now,
            });

            return;
        }

        user.DisplayName = displayName;
        user.Login = login;
        user.Role = settings.Role.ToString();
        user.UpdatedAt = now;
    }

    private static async Task<string> MakeUniqueLoginAsync(
        ProximaDbContext ctx,
        Guid ownerUserId,
        string login,
        CancellationToken cancellationToken)
    {
        string normalized = string.IsNullOrWhiteSpace(login)
            ? $"user-{ownerUserId:N}"
            : login.Trim().ToLowerInvariant();

        bool usedByOtherUser = await ctx.Users.AsNoTracking()
            .AnyAsync(x => x.Id != ownerUserId && x.Login == normalized, cancellationToken)
            .ConfigureAwait(false);

        if (!usedByOtherUser)
        {
            return normalized;
        }

        string suffix = ownerUserId.ToString("N")[..8];
        return $"{normalized}-{suffix}";
    }

    private static string NormalizeDisplayName(string displayName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? "Пользователь" : displayName.Trim();
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
            ParseQuoteProvider(x.QuoteProvider),
            x.QuoteRefreshMinutes,
            x.TwelveDataApiKeyProtected,
            Enum.Parse<CurrencyProviderKind>(x.CurrencyProvider, true));
    }

    private static QuoteProviderKind ParseQuoteProvider(string value)
    {
        return Enum.TryParse(value, ignoreCase: true, out QuoteProviderKind parsed)
            && Enum.IsDefined(typeof(QuoteProviderKind), parsed)
            ? parsed
            : QuoteProviderKind.TwelveData;
    }

    private static UserSettingsEntity ToEntity(UserSettings settings)
    {
        return new UserSettingsEntity
        {
            OwnerUserId = settings.OwnerUserId,
            DisplayName = NormalizeDisplayName(settings.DisplayName),
            Role = settings.Role.ToString(),
            Login = string.IsNullOrWhiteSpace(settings.Login) ? string.Empty : settings.Login.Trim().ToLowerInvariant(),
            PreferredCurrency = settings.PreferredCurrency,
            Language = settings.Language.ToString(),
            UiScale = settings.UiScale,
            QuoteProvider = settings.QuoteProvider.ToString(),
            QuoteRefreshMinutes = settings.QuoteRefreshMinutes,
            TwelveDataApiKeyProtected = settings.TwelveDataApiKeyProtected,
            CurrencyProvider = settings.CurrencyProvider.ToString(),
        };
    }
}
