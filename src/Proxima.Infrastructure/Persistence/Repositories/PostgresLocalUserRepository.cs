using Microsoft.EntityFrameworkCore;
using Proxima.Core.Application.Auth;
using Proxima.Core.Domain.Auth;

namespace Proxima.Infrastructure.Persistence.Repositories;

public sealed class PostgresLocalUserRepository(
    IProximaUnitOfWorkFactory uowFactory,
    IProximaUnitOfWorkAccessor uowAccessor)
    : ILocalUserRepository
{
    public async Task<bool> HasAnyProfileAsync(CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        return await lease.Context.Users
            .AsNoTracking()
            .AnyAsync(x => x.PasswordHash != string.Empty, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LocalUserProfile?> FindByLoginAsync(string login, CancellationToken cancellationToken)
    {
        string normalizedLogin = NormalizeLogin(login);
        if (string.IsNullOrWhiteSpace(normalizedLogin))
        {
            return null;
        }

        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        UserEntity? entity = await lease.Context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Login == normalizedLogin && x.PasswordHash != string.Empty, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(LocalUserProfile profile, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        string normalizedLogin = NormalizeLogin(profile.Login);

        bool exists = await ctx.Users
            .AsNoTracking()
            .AnyAsync(x => x.Login == normalizedLogin, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            throw new InvalidOperationException("A local profile with the same login already exists.");
        }

        ctx.Users.Add(ToEntity(profile with { Login = normalizedLogin }));
        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(LocalUserProfile profile, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;
        UserEntity entity = await ctx.Users
            .FirstAsync(x => x.Id == profile.Id, cancellationToken)
            .ConfigureAwait(false);

        string normalizedLogin = NormalizeLogin(profile.Login);
        bool loginTaken = await ctx.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id != profile.Id && x.Login == normalizedLogin, cancellationToken)
            .ConfigureAwait(false);

        if (loginTaken)
        {
            throw new InvalidOperationException("A local profile with the same login already exists.");
        }

        entity.DisplayName = profile.DisplayName.Trim();
        entity.Login = normalizedLogin;
        entity.Role = profile.Role.ToString();
        entity.PasswordHash = profile.Credential.EncodedHash;
        entity.FailedUnlockAttempts = profile.FailedUnlockAttempts;
        entity.UpdatedAt = profile.UpdatedAt;

        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid profileId, CancellationToken cancellationToken)
    {
        if (profileId == Guid.Empty)
        {
            return;
        }

        await uowFactory.ExecuteInTransactionAsync(async uow =>
        {
            ProximaDbContext ctx = uow.Context;

            Guid[] portfolioIds = await ctx.Portfolios
                .Where(x => x.OwnerUserId == profileId)
                .Select(x => x.Id)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            Guid[] assetIds = portfolioIds.Length == 0
                ? []
                : await ctx.Assets
                    .Where(x => portfolioIds.Contains(x.PortfolioId))
                    .Select(x => x.Id)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (assetIds.Length > 0)
            {
                Guid[] tagIds = await ctx.AssetTags
                    .Where(x => assetIds.Contains(x.AssetId))
                    .Select(x => x.TagId)
                    .Distinct()
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

                await ctx.AssetTags
                    .Where(x => assetIds.Contains(x.AssetId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (tagIds.Length > 0)
                {
                    await ctx.Tags
                        .Where(x => tagIds.Contains(x.Id) && !ctx.AssetTags.Any(link => link.TagId == x.Id))
                        .ExecuteDeleteAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                await ctx.QuoteCache
                    .Where(x => assetIds.Contains(x.AssetId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                await ctx.AssetPrices
                    .Where(x => assetIds.Contains(x.AssetId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (portfolioIds.Length > 0)
            {
                await ctx.Goals
                    .Where(x => portfolioIds.Contains(x.PortfolioId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                await ctx.Transactions
                    .Where(x => portfolioIds.Contains(x.PortfolioId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                await ctx.Assets
                    .Where(x => portfolioIds.Contains(x.PortfolioId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                await ctx.Portfolios
                    .Where(x => x.OwnerUserId == profileId)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            await ctx.UserSettings
                .Where(x => x.OwnerUserId == profileId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            await ctx.Notifications
                .Where(x => x.UserId == profileId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            await ctx.AuditLog
                .Where(x => x.UserId == profileId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            await ctx.Users
                .Where(x => x.Id == profileId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static LocalUserProfile ToDomain(UserEntity entity)
    {
        return new LocalUserProfile(
            entity.Id,
            entity.DisplayName,
            entity.Login,
            Enum.Parse<UserRole>(entity.Role, ignoreCase: true),
            new PasswordCredential(entity.PasswordHash),
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.FailedUnlockAttempts);
    }

    private static UserEntity ToEntity(LocalUserProfile profile)
    {
        return new UserEntity
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName.Trim(),
            Login = NormalizeLogin(profile.Login),
            Role = profile.Role.ToString(),
            PasswordHash = profile.Credential.EncodedHash,
            FailedUnlockAttempts = profile.FailedUnlockAttempts,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
        };
    }

    private static string NormalizeLogin(string login)
    {
        return string.IsNullOrWhiteSpace(login) ? string.Empty : login.Trim().ToLowerInvariant();
    }
}
