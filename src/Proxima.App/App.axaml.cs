using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Proxima.App.ViewModels;
using Proxima.Infrastructure.Assets;
using Proxima.Infrastructure.Auth;
using Proxima.Infrastructure.Portfolios;
using Proxima.Infrastructure.Transactions;

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
            string portfolioStorePath = ProximaPortfolioComposition.GetDefaultPortfolioStorePath();
            string assetStorePath = ProximaAssetComposition.GetDefaultAssetStorePath();
            string transactionStorePath = ProximaTransactionComposition.GetDefaultTransactionStorePath();
            ShellViewModel shell = new(
                new ShellNavigationService(),
                ProximaPortfolioComposition.CreatePortfolioService(portfolioStorePath),
                ProximaAssetComposition.CreateAssetService(assetStorePath),
                ProximaTransactionComposition.CreateTransactionService(transactionStorePath, assetStorePath));
            desktop.MainWindow = new MainWindow(new AuthViewModel(ProximaAuthComposition.CreateLocalAuthService(profileStorePath), shell));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
