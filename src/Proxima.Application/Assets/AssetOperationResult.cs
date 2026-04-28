using Proxima.Domain.Assets;

namespace Proxima.Application.Assets;

public sealed record AssetOperationResult(bool Succeeded, string Message, Asset? Asset)
{
    public static AssetOperationResult Success(Asset asset)
    {
        return new AssetOperationResult(true, string.Empty, asset);
    }

    public static AssetOperationResult Failure(string message)
    {
        return new AssetOperationResult(false, message, null);
    }
}
