using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Proxima.App.ViewModels;
using Proxima.Infrastructure.Auth;

namespace Proxima.App;

public partial class App : global::Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string profileStorePath = ProximaAuthComposition.GetDefaultProfileStorePath();
            desktop.MainWindow = new MainWindow(new AuthViewModel(ProximaAuthComposition.CreateLocalAuthService(profileStorePath)));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
