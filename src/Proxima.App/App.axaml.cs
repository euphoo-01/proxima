using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Proxima.App.Composition;
using Proxima.App.Shell;
using Proxima.App.ViewModels;

namespace Proxima.App;

public partial class App : global::Avalonia.Application
{
#if DEBUG
    private static readonly bool DevAutoLogin =
        Environment.GetEnvironmentVariable("PROXIMA_DEV_AUTO_LOGIN") != "0";
#else
    private const bool DevAutoLogin = false;
#endif

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ServiceProvider services = AppComposition.BuildServiceProvider();

            if (DevAutoLogin)
            {
                desktop.MainWindow = CreateAppShellWindow(services);
            }
            else
            {
                AuthViewModel authViewModel = services.GetRequiredService<AuthViewModel>();
                MainWindow loginWindow = new(authViewModel);
                authViewModel.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(AuthViewModel.IsUnlocked) && authViewModel.IsUnlocked)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            Window appShellWindow = CreateAppShellWindow(services);
                            desktop.MainWindow = appShellWindow;
                            appShellWindow.Show();
                            loginWindow.Close();
                        });
                    }
                };

                desktop.MainWindow = loginWindow;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Window CreateAppShellWindow(ServiceProvider services)
    {
        AppShellView appShellView = services.GetRequiredService<AppShellView>();
        appShellView.DataContext = services.GetRequiredService<AppShellViewModel>();

        return new Window
        {
            Title = "Proxima",
            Width = 1280,
            Height = 900,
            MinWidth = 1120,
            MinHeight = 720,
            Background = Brushes.Transparent,
            Content = appShellView,
        };
    }
}
