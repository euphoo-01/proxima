namespace Proxima.App.Views.Goals;

public static class GoalsDesignData
{
    public static GoalsViewModel Sample { get; } = CreateSample();

    private static GoalsViewModel CreateSample()
    {
        GoalsViewModel vm = GoalsViewModel.CreateDesignData();
        vm.MonthlyContribution = 450m;
        return vm;
    }
}
