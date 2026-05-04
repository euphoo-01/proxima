using Proxima.App.ViewModels;

namespace Proxima.App.Shell;

public sealed class TopbarViewModel : ViewModelBase
{
    private string _title = "Dashboard";
    private string _breadcrumb = "Dashboard";
    private string _currentPortfolio = "Demo Portfolio";

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Breadcrumb
    {
        get => _breadcrumb;
        private set => SetProperty(ref _breadcrumb, value);
    }

    public string CurrentPortfolio
    {
        get => _currentPortfolio;
        private set => SetProperty(ref _currentPortfolio, value);
    }

    public void Update(string title, string breadcrumb, string currentPortfolio)
    {
        Title = title;
        Breadcrumb = breadcrumb;
        CurrentPortfolio = currentPortfolio;
    }
}
