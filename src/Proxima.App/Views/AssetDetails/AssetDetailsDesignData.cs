namespace Proxima.App.Views.AssetDetails;

public static class AssetDetailsDesignData
{
    public static AssetDetailsViewModel Sample { get; } = CreateSample();

    private static AssetDetailsViewModel CreateSample()
    {
        AssetDetailsViewModel vm = AssetDetailsViewModel.CreateDesignData();
        vm.SelectedTimeframe = "30д";
        return vm;
    }
}
