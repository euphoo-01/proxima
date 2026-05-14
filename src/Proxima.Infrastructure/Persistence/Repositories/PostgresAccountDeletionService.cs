using Microsoft.EntityFrameworkCore;
using Proxima.Application.Auth;

namespace Proxima.Infrastructure.Persistence.Repositories;

public sealed class PostgresAccountDeletionService(
    IProximaUnitOfWorkFactory uowFactory,
    IProximaUnitOfWorkAccessor uowAccessor)
    : IAccountDeletionService
{
    public async Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;

        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"delete from audit_log where user_id = {userId};",
            cancellationToken).ConfigureAwait(false);

        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"delete from user_settings where owner_user_id = {userId};",
            cancellationToken).ConfigureAwait(false);

        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"delete from goals where portfolio_id in (select id from portfolios where owner_user_id = {userId});",
            cancellationToken).ConfigureAwait(false);

        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"delete from transactions where portfolio_id in (select id from portfolios where owner_user_id = {userId});",
            cancellationToken).ConfigureAwait(false);

        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"delete from quote_cache where asset_id in (select a.id from assets a join portfolios p on p.id = a.portfolio_id where p.owner_user_id = {userId});",
            cancellationToken).ConfigureAwait(false);

        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"delete from asset_prices where asset_id in (select a.id from assets a join portfolios p on p.id = a.portfolio_id where p.owner_user_id = {userId});",
            cancellationToken).ConfigureAwait(false);

        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"delete from asset_tags where asset_id in (select a.id from assets a join portfolios p on p.id = a.portfolio_id where p.owner_user_id = {userId});",
            cancellationToken).ConfigureAwait(false);

        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"delete from assets where portfolio_id in (select id from portfolios where owner_user_id = {userId});",
            cancellationToken).ConfigureAwait(false);

        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"delete from portfolios where owner_user_id = {userId};",
            cancellationToken).ConfigureAwait(false);

        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"delete from users where id = {userId};",
            cancellationToken).ConfigureAwait(false);
    }
}
