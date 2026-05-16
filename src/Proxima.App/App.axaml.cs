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
using Proxima.Core.Application.Auth;
using Proxima.Core.Application.Settings;
using Proxima.Core.Domain.Auth;
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
            RegisterAuthResetNavigation(desktop, services, runtimeUserContext);

            RuntimeAuthBootstrapResult bootstrap = authBootstrapper.EnsureRuntimeProfileAsync().GetAwaiter().GetResult();
            if (bootstrap.IsFirstRunRequired)
            {
                desktop.MainWindow = CreateRegisterWindow(desktop, services, runtimeUserContext);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            if (DevAutoLogin)
            {
                AuthResult autoLogin = authBootstrapper.TryDevAutoLoginAsync().GetAwaiter().GetResult();
                if (autoLogin.Succeeded && autoLogin.Profile is not null)
                {
                    runtimeUserContext.SetAuthenticated(autoLogin.Profile);
                    EnsureSettingsForProfile(services, autoLogin.Profile);
                    desktop.MainWindow = CreateAppShellWindow(services);
                }
                else
                {
                    desktop.MainWindow = CreateLoginWindow(desktop, services);
                }
            }
            else
            {
                desktop.MainWindow = CreateLoginWindow(desktop, services);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterAuthResetNavigation(
        IClassicDesktopStyleApplicationLifetime desktop,
        ServiceProvider services,
        IRuntimeUserContext runtimeUserContext)
    {
        runtimeUserContext.ProfileChanged += (_, _) =>
        {
            if (runtimeUserContext.IsAuthenticated)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                Window? currentWindow = desktop.MainWindow;
                if (currentWindow?.Content is RegisterView or LoginView)
                {
                    return;
                }

                Window registerWindow = CreateRegisterWindow(desktop, services, runtimeUserContext);
                desktop.MainWindow = registerWindow;
                registerWindow.Show();
                currentWindow?.Close();
            });
        };
    }

    private static void EnsureSettingsForProfile(ServiceProvider services, LocalUserProfile profile)
    {
        ISettingsService settingsService = services.GetRequiredService<ISettingsService>();
        settingsService.EnsureAsync(new CreateDefaultSettingsRequest(profile.Id))
            .GetAwaiter()
            .GetResult();
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
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
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
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new TextBlock
            {
                Margin = new Thickness(24),
                Text = $"PostgreSQL startup failed.\n{message}\n\nCheck docker-compose and PROXIMA_DB_CONNECTION.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 16,
            },
        };
    }

    private static Window CreateLoginWindow(IClassicDesktopStyleApplicationLifetime desktop, ServiceProvider services)
    {
        LoginViewModel loginViewModel = services.GetRequiredService<LoginViewModel>();
        LoginView loginView = new() { DataContext = loginViewModel };

        Window loginWindow = CreateAuthWindow("Proxima — Login", loginView);

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

        loginViewModel.RegisterRequested += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                IRuntimeUserContext runtimeUserContext = services.GetRequiredService<IRuntimeUserContext>();
                Window registerWindow = CreateRegisterWindow(desktop, services, runtimeUserContext);
                desktop.MainWindow = registerWindow;
                registerWindow.Show();
                loginWindow.Close();
            });
        };

        return loginWindow;
    }

    private static Window CreateRegisterWindow(
        IClassicDesktopStyleApplicationLifetime desktop,
        ServiceProvider services,
        IRuntimeUserContext runtimeUserContext)
    {
        RegisterViewModel registerViewModel = services.GetRequiredService<RegisterViewModel>();
        RegisterView registerView = new() { DataContext = registerViewModel };

        Window registerWindow = CreateAuthWindow("Proxima — Register", registerView);

        registerViewModel.Registered += (_, profile) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                runtimeUserContext.SetAuthenticated(profile);
                EnsureSettingsForProfile(services, profile);
                Window appShellWindow = CreateAppShellWindow(services);
                desktop.MainWindow = appShellWindow;
                appShellWindow.Show();
                registerWindow.Close();
            });
        };

        registerViewModel.LoginRequested += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                Window loginWindow = CreateLoginWindow(desktop, services);
                desktop.MainWindow = loginWindow;
                loginWindow.Show();
                registerWindow.Close();
            });
        };

        return registerWindow;
    }

    private static Window CreateAuthWindow(string title, Control content)
    {
        return new Window
        {
            Title = title,
            Width = 1280,
            Height = 720,
            MinWidth = 960,
            MinHeight = 640,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Color.Parse("#F7F8F9")),
            Content = content,
        };
    }
}
