namespace Proxima.App.ViewModels;

public sealed class ShellNavigationService
{
    private readonly Dictionary<string, ShellRoute> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<string> _history = new();

    public string CurrentRoute { get; private set; } = string.Empty;

    public bool CanGoBack => _history.Count > 0;

    public void Register(ShellRoute route)
    {
        _routes[route.Route] = route;
    }

    public IReadOnlyCollection<ShellRoute> Routes => _routes.Values;

    public ShellRoute Navigate(string route, bool pushHistory = true)
    {
        if (!_routes.TryGetValue(route, out ShellRoute? destination))
        {
            throw new InvalidOperationException($"Unknown route: {route}");
        }

        if (pushHistory && !string.IsNullOrWhiteSpace(CurrentRoute))
        {
            _history.Push(CurrentRoute);
        }

        CurrentRoute = destination.Route;
        return destination;
    }

    public ShellRoute GoBack()
    {
        if (_history.Count == 0)
        {
            throw new InvalidOperationException("There is no back route in history.");
        }

        string previousRoute = _history.Pop();
        return Navigate(previousRoute, pushHistory: false);
    }
}
