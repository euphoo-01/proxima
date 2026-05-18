using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.Notifications;
using Proxima.App.ViewModels;
using Proxima.App.Common.Commands;

namespace Proxima.App.Views.Notifications;

public sealed class NotificationsViewModel : ViewModelBase
{
    private readonly IAppNotificationCenter _notificationCenter;
    private readonly AsyncCommand _refreshCommand;
    private readonly AsyncCommand _clearAllCommand;
    private readonly AsyncParameterCommand _deleteCommand;
    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public NotificationsViewModel(IAppNotificationCenter notificationCenter)
    {
        _notificationCenter = notificationCenter;
        Items = [];

        _refreshCommand = new AsyncCommand(LoadAsync, () => !IsLoading);
        _clearAllCommand = new AsyncCommand(ClearAllAsync, () => !IsLoading && Items.Count > 0);
        _deleteCommand = new AsyncParameterCommand(DeleteAsync, _ => !IsLoading);

        _notificationCenter.NotificationsChanged += (_, _) => _ = LoadAsync();
        _ = LoadAsync();
    }

    public ObservableCollection<AppNotificationViewModel> Items { get; }

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand ClearAllCommand => _clearAllCommand;

    public ICommand DeleteCommand => _deleteCommand;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(IsEmpty));
                _refreshCommand.RaiseCanExecuteChanged();
                _clearAllCommand.RaiseCanExecuteChanged();
                _deleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasItems => !IsLoading && Items.Count > 0;

    public bool IsEmpty => !IsLoading && Items.Count == 0;

    public string CountText => Items.Count switch
    {
        0 => "Нет активных уведомлений",
        1 => "1 активное уведомление",
        int count when count is >= 2 and <= 4 => $"{count} активных уведомления",
        int count => $"{count} активных уведомлений",
    };

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            IReadOnlyList<AppNotificationViewModel> notifications = await _notificationCenter.ListAsync().ConfigureAwait(true);
            Items.Clear();
            foreach (AppNotificationViewModel notification in notifications)
            {
                Items.Add(notification);
            }

            StatusMessage = Items.Count == 0
                ? "Все чисто. Новые ошибки, предупреждения и успешные действия появятся здесь автоматически."
                : "Уведомления хранятся здесь, пока вы не удалите их вручную.";
            RaiseCollectionComputedProperties();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось загрузить уведомления: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteAsync(object? parameter)
    {
        Guid id = parameter switch
        {
            AppNotificationViewModel notification => notification.Id,
            Guid value => value,
            _ => Guid.Empty,
        };

        if (id == Guid.Empty)
        {
            return;
        }

        await _notificationCenter.DeleteAsync(id).ConfigureAwait(true);
        AppNotificationViewModel? existing = Items.FirstOrDefault(item => item.Id == id);
        if (existing is not null)
        {
            Items.Remove(existing);
        }

        RaiseCollectionComputedProperties();
    }

    private async Task ClearAllAsync()
    {
        await _notificationCenter.ClearAsync().ConfigureAwait(true);
        Items.Clear();
        StatusMessage = "Все уведомления удалены.";
        RaiseCollectionComputedProperties();
    }

    private void RaiseCollectionComputedProperties()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CountText));
        _clearAllCommand.RaiseCanExecuteChanged();
    }


}
