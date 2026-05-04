using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Proxima.App.Composition;
using Proxima.App.Shell;
using Proxima.App.Views.Auth;
using Proxima.Domain.Auth;
using Proxima.Infrastructure.Persistence;

namespace Proxima.App;

public partial class App : global::Avalonia.Application
{
    private static readonly bool DevAutoLogin =
        Environment.GetEnvironmentVariable("PROXIMA_DEV_AUTO_LOGIN") != "0";


    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ServiceProvider services = AppComposition.BuildServiceProvider();
            DatabaseBootstrapService dbBootstrap = services.GetRequiredService<DatabaseBootstrapService>();
            string dbStatus = dbBootstrap.EnsureReadyAsync().GetAwaiter().GetResult();
            if (!dbStatus.StartsWith("Database ready", StringComparison.OrdinalIgnoreCase))
            {
                desktop.MainWindow = CreateDatabaseUnavailableWindow(dbStatus);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            IRuntimeAuthBootstrapper authBootstrapper = services.GetRequiredService<IRuntimeAuthBootstrapper>();
            IRuntimeUserContext runtimeUserContext = services.GetRequiredService<IRuntimeUserContext>();

            if (DevAutoLogin)
            {
                LocalUserProfile profile = authBootstrapper.EnsureRuntimeProfileAsync().GetAwaiter().GetResult();
                runtimeUserContext.SetAuthenticated(profile);
                desktop.MainWindow = CreateAppShellWindow(services);
            }
            else
            {
                _ = authBootstrapper.EnsureRuntimeProfileAsync().GetAwaiter().GetResult();
                LoginViewModel loginViewModel = services.GetRequiredService<LoginViewModel>();
                LoginView loginView = new() { DataContext = loginViewModel };

                Window loginWindow = new()
                {
                    Title = "Proxima — Login",
                    Width = 960,
                    Height = 720,
                    MinWidth = 860,
                    MinHeight = 640,
                    Content = loginView,
                };

                loginViewModel.Unlocked += (_, _) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Window appShellWindow = CreateAppShellWindow(services);
                        desktop.MainWindow = appShellWindow;
                        appShellWindow.Show();
                        loginWindow.Close();
                    });
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

    private static Window CreateDatabaseUnavailableWindow(string message)
    {
        return new Window
        {
            Title = "Proxima — Database unavailable",
            Width = 860,
            Height = 540,
            MinWidth = 760,
            MinHeight = 480,
            Content = new TextBlock
            {
                Margin = new Thickness(24),
                Text = $"PostgreSQL startup failed.\\n{message}\\n\\nCheck docker-compose and PROXIMA_DB_CONNECTION.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 16,
            },
        };
    }
}
