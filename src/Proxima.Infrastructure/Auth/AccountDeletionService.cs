using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Proxima.Application.Auth;
using Proxima.Domain.Auth;
using Proxima.Infrastructure.Persistence;

namespace Proxima.Infrastructure.Auth;

public sealed class AccountDeletionService(
    IProximaUnitOfWorkFactory uowFactory,
    IProximaUnitOfWorkAccessor uowAccessor,
    string localProfileStorePath)
    : IAccountDeletionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        await DeleteDatabaseDataAsync(userId, cancellationToken).ConfigureAwait(false);
        await DeleteLocalProfileAsync(userId, string.Empty, cancellationToken).ConfigureAwait(false);
    }

    private async Task DeleteDatabaseDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using IProximaUnitOfWork uow = uowFactory.Create();
        IProximaUnitOfWork? previous = uowAccessor.Current;
        uowAccessor.Current = uow;

        try
        {
            ProximaDbContext ctx = uow.Context;

            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx = await ctx.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            List<Guid> portfolioIds = await ctx.Portfolios
                .Where(x => x.OwnerUserId == userId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<Guid> assetIds = portfolioIds.Count == 0
                ? []
                : await ctx.Assets
                    .Where(x => portfolioIds.Contains(x.PortfolioId))
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

            List<Guid> importSessionIds = portfolioIds.Count == 0
                ? []
                : await ctx.ImportSessions
                    .Where(x => portfolioIds.Contains(x.PortfolioId))
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (importSessionIds.Count > 0)
            {
                await ctx.ImportRows
                    .Where(x => importSessionIds.Contains(x.ImportSessionId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                await ctx.ImportSessions
                    .Where(x => importSessionIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (portfolioIds.Count > 0)
            {
                await ctx.Transactions
                    .Where(x => portfolioIds.Contains(x.PortfolioId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                await ctx.Goals
                    .Where(x => portfolioIds.Contains(x.PortfolioId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                await ctx.TaxReports
                    .Where(x => portfolioIds.Contains(x.PortfolioId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (assetIds.Count > 0)
            {
                await ctx.AssetTags
                    .Where(x => assetIds.Contains(x.AssetId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                await ctx.AssetPrices
                    .Where(x => assetIds.Contains(x.AssetId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                await ctx.QuoteCache
                    .Where(x => assetIds.Contains(x.AssetId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                await ctx.Assets
                    .Where(x => assetIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (portfolioIds.Count > 0)
            {
                await ctx.Portfolios
                    .Where(x => portfolioIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            await ctx.TaxProfiles
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            await ctx.SyncSnapshots
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            await ctx.UserSettings
                .Where(x => x.OwnerUserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            await ctx.AuditLog
                .Where(x => x.UserId == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.UserId, (Guid?)null),
                    cancellationToken)
                .ConfigureAwait(false);

            await ctx.Users
                .Where(x => x.Id == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            uowAccessor.Current = previous;
        }
    }

    private async Task DeleteLocalProfileAsync(Guid userId, string login, CancellationToken cancellationToken)
    {
        if (!File.Exists(localProfileStorePath))
        {
            return;
        }

        await using FileStream readStream = File.OpenRead(localProfileStorePath);
        List<LocalUserProfile> profiles = await JsonSerializer
            .DeserializeAsync<List<LocalUserProfile>>(readStream, JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? [];

        string normalizedLogin = string.IsNullOrWhiteSpace(login) ? string.Empty : login.Trim().ToLowerInvariant();
        int removed = profiles.RemoveAll(x =>
            x.Id == userId
            || (!string.IsNullOrWhiteSpace(normalizedLogin)
                && string.Equals(x.Login, normalizedLogin, StringComparison.OrdinalIgnoreCase)));

        if (removed == 0)
        {
            return;
        }

        string? directory = Path.GetDirectoryName(localProfileStorePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempFile = localProfileStorePath + ".tmp";
        await using (FileStream writeStream = File.Create(tempFile))
        {
            await JsonSerializer
                .SerializeAsync(writeStream, profiles, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempFile, localProfileStorePath, overwrite: true);
    }
}
