using Microsoft.EntityFrameworkCore;
using Proxima.Core.Application.Notifications;
using Proxima.Core.Domain.Notifications;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Notifications;

public sealed class PostgresNotificationRepository(
    IProximaUnitOfWorkFactory uowFactory,
    IProximaUnitOfWorkAccessor uowAccessor) : INotificationRepository
{
    public async Task<IReadOnlyList<UserNotification>> ListActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;

        return await ctx.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.DeletedAtUtc == null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new UserNotification(
                x.Id,
                x.UserId,
                ParseSeverity(x.Severity),
                x.Title,
                x.Message,
                x.Source,
                x.CreatedAtUtc,
                x.DeletedAtUtc))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(UserNotification notification, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;

        ctx.Notifications.Add(new NotificationEntity
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Severity = notification.Severity.ToString(),
            Title = notification.Title,
            Message = notification.Message,
            Source = notification.Source,
            CreatedAtUtc = notification.CreatedAtUtc,
            DeletedAtUtc = notification.DeletedAtUtc,
        });

        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkDeletedAsync(Guid userId, Guid notificationId, DateTimeOffset deletedAtUtc, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;

        NotificationEntity? entity = await ctx.Notifications
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == notificationId && x.DeletedAtUtc == null, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        entity.DeletedAtUtc = deletedAtUtc;
        await lease.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkAllDeletedAsync(Guid userId, DateTimeOffset deletedAtUtc, CancellationToken cancellationToken)
    {
        await using UowLease lease = UowLease.Create(uowFactory, uowAccessor);
        ProximaDbContext ctx = lease.Context;

        await ctx.Notifications
            .Where(x => x.UserId == userId && x.DeletedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.DeletedAtUtc, deletedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static NotificationSeverity ParseSeverity(string value)
    {
        return Enum.TryParse(value, true, out NotificationSeverity severity)
            ? severity
            : NotificationSeverity.Info;
    }
}
