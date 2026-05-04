namespace Proxima.App.Shell;

public interface IRuntimeDataInvalidation
{
    event EventHandler<RuntimeDataInvalidatedEventArgs>? DataInvalidated;

    void Invalidate(string reason);
}

public sealed record RuntimeDataInvalidatedEventArgs(string Reason);

public sealed class RuntimeDataInvalidation : IRuntimeDataInvalidation
{
    public event EventHandler<RuntimeDataInvalidatedEventArgs>? DataInvalidated;

    public void Invalidate(string reason)
    {
        string message = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason.Trim();
        DataInvalidated?.Invoke(this, new RuntimeDataInvalidatedEventArgs(message));
    }
}
