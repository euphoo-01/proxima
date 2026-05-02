using Proxima.App.ViewModels;

namespace Proxima.App.Shell;

public sealed class TopbarViewModel : ViewModelBase
{
    private string _currentSection = "Панель";
    public string CurrentSection
    {
        get => _currentSection;
        private set => SetProperty(ref _currentSection, value);
    }

    public string BreadcrumbRoot => "Proxima";

    public string Breadcrumb => $"{BreadcrumbRoot} / {CurrentSection}";

    public void SetCurrentSection(string section)
    {
        if (SetProperty(ref _currentSection, section, nameof(CurrentSection)))
        {
            OnPropertyChanged(nameof(Breadcrumb));
        }
    }
}
