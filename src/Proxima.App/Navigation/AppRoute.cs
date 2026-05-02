namespace Proxima.App.Navigation;

public sealed record AppRoute(
    string Key,
    string Title,
    string Breadcrumb,
    IReadOnlyDictionary<string, string>? Parameters = null);
