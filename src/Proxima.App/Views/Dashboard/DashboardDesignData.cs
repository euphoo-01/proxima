namespace Proxima.App.Views.Dashboard;

public static class DashboardDesignData
{
    public static DashboardViewModel Sample { get; } = CreateSample();

    private static DashboardViewModel CreateSample()
    {
        DashboardViewModel vm = DashboardViewModel.CreateDesignData();
        vm.SelectedTimeframe = "месяц";
        return vm;
    }
}
