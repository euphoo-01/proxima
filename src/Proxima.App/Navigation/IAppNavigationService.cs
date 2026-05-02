namespace Proxima.App.Navigation;

public interface IAppNavigationService
{
    AppRoute Current { get; }
    IReadOnlyList<AppRoute> Routes { get; }
    event Action<AppRoute>? RouteChanged;

    void Register(AppRoute route);
    void Navigate(string routeKey);
}
