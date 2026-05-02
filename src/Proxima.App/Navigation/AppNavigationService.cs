namespace Proxima.App.Navigation;

public sealed class AppNavigationService : IAppNavigationService
{
    private readonly Dictionary<string, AppRoute> _routes = new(StringComparer.OrdinalIgnoreCase);

    public AppRoute Current { get; private set; } = new(AppRoutes.Dashboard, "Dashboard", "Dashboard");

    public IReadOnlyList<AppRoute> Routes => _routes.Values.ToList();

    public event Action<AppRoute>? RouteChanged;

    public void Register(AppRoute route)
    {
        _routes[route.Key] = route;
    }

    public void Navigate(string routeKey)
    {
        if (!_routes.TryGetValue(routeKey, out AppRoute? route))
        {
            throw new InvalidOperationException($"Unknown route '{routeKey}'.");
        }

        Current = route;
        RouteChanged?.Invoke(route);
    }
}
