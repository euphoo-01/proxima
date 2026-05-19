using System.Collections.ObjectModel;
using Avalonia.Threading;
using Proxima.App.ViewModels;
using Proxima.App.Auth;
using Proxima.Core.Application.Notifications;
using Proxima.Core.Domain.Notifications;

namespace Proxima.App.Notifications;

public sealed class AppNotificationCenter : IAppNotificationCenter
{
    private static readonly TimeSpan ToastLifetime = TimeSpan.FromSeconds(6);

    private readonly INotificationService _notificationService;
    private readonly IRuntimeUserContext _userContext;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AppNotificationCenter(INotificationService notificationService, IRuntimeUserContext userContext)
    {
        _notificationService = notificationService;
        _userContext = userContext;
        Toasts = [];
    }

    public ObservableCollection<AppNotificationViewModel> Toasts { get; }

    public event EventHandler? NotificationsChanged;

    public async Task NotifyAsync(
        AppNotificationLevel level,
        string title,
        string message,
        string? source = null,
        bool persist = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        AppNotificationViewModel toast;
        bool persisted = false;

        if (persist && _userContext.IsAuthenticated && _userContext.UserId != Guid.Empty)
        {
            try
            {
                UserNotification notification = await _notificationService.CreateAsync(
                    new CreateNotificationRequest(
                        _userContext.UserId,
                        AppNotificationViewModel.ToDomainSeverity(level),
                        title,
                        message,
                        source),
                    cancellationToken).ConfigureAwait(true);

                toast = AppNotificationViewModel.FromDomain(notification);
                persisted = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                toast = new AppNotificationViewModel(
                    Guid.NewGuid(),
                    AppNotificationLevel.Warning,
                    "Уведомление не сохранено",
                    $"{message.Trim()}\n\nНе удалось записать уведомление в БД. Оно показано только всплывающим сообщением.",
                    source ?? "Proxima",
                    DateTimeOffset.UtcNow,
                    isPersistent: false);
            }
        }
        else
        {
            toast = new AppNotificationViewModel(
                Guid.NewGuid(),
                level,
                title,
                message,
                source ?? "Proxima",
                DateTimeOffset.UtcNow,
                isPersistent: false);
        }

        await AddToastAsync(toast).ConfigureAwait(true);

        if (persisted)
        {
            NotificationsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<IReadOnlyList<AppNotificationViewModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty)
        {
            return [];
        }

        IReadOnlyList<UserNotification> notifications = await _notificationService
            .ListActiveAsync(_userContext.UserId, cancellationToken)
            .ConfigureAwait(true);

        return notifications.Select(AppNotificationViewModel.FromDomain).ToArray();
    }

    public async Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty || notificationId == Guid.Empty)
        {
            return;
        }

        await _notificationService.DeleteAsync(_userContext.UserId, notificationId, cancellationToken).ConfigureAwait(true);
        NotificationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty)
        {
            return;
        }

        await _notificationService.DeleteAllAsync(_userContext.UserId, cancellationToken).ConfigureAwait(true);
        NotificationsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task AddToastAsync(AppNotificationViewModel toast)
    {
        await _gate.WaitAsync().ConfigureAwait(true);
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Toasts.Insert(0, toast);
                while (Toasts.Count > 4)
                {
                    Toasts.RemoveAt(Toasts.Count - 1);
                }
            });
        }
        finally
        {
            _gate.Release();
        }

        _ = RemoveToastLaterAsync(toast.Id);
    }

    private async Task RemoveToastLaterAsync(Guid id)
    {
        await Task.Delay(ToastLifetime).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            AppNotificationViewModel? item = Toasts.FirstOrDefault(x => x.Id == id);
            if (item is not null)
            {
                Toasts.Remove(item);
            }
        });
    }
}
