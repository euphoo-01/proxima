using Microsoft.EntityFrameworkCore;
using Proxima.Application.Auth;
using Proxima.Domain.Auth;

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
            .AnyAsync(x => x.PasswordIterations > 0 && x.PasswordVersion > 0, cancellationToken)
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
            .FirstOrDefaultAsync(x => x.Login == normalizedLogin && x.PasswordIterations > 0 && x.PasswordVersion > 0, cancellationToken)
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
        entity.PasswordAlgorithm = profile.Credential.Algorithm;
        entity.PasswordSalt = profile.Credential.Salt;
        entity.PasswordHash = profile.Credential.Hash;
        entity.PasswordIterations = profile.Credential.Iterations;
        entity.PasswordVersion = profile.Credential.Version;
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

        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        await lease.Context.Users
            .Where(x => x.Id == profileId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static LocalUserProfile ToDomain(UserEntity entity)
    {
        return new LocalUserProfile(
            entity.Id,
            entity.DisplayName,
            entity.Login,
            Enum.Parse<UserRole>(entity.Role, ignoreCase: true),
            new PasswordCredential(
                entity.PasswordAlgorithm,
                entity.PasswordSalt,
                entity.PasswordHash,
                entity.PasswordIterations,
                entity.PasswordVersion),
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
            PasswordAlgorithm = profile.Credential.Algorithm,
            PasswordSalt = profile.Credential.Salt,
            PasswordHash = profile.Credential.Hash,
            PasswordIterations = profile.Credential.Iterations,
            PasswordVersion = profile.Credential.Version,
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
