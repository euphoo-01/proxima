using Proxima.App.ViewModels;

namespace Proxima.App.ViewModels.Placeholder;

public sealed class PlaceholderViewModel : ViewModelBase
{
    public PlaceholderViewModel(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }

    public string Description { get; }
}
