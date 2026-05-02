using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.Shell;
using Proxima.App.ViewModels;
using Proxima.Application.Goals;
using Proxima.Domain.Goals;

namespace Proxima.App.Views.Goals;

public sealed class GoalsViewModel : ViewModelBase
{
    private readonly IGoalService _goalService;
    private readonly IShellState _shellState;
    private readonly IGoalProjectionService _projectionService;
    private readonly DelegateCommand _openAddGoalCommand;
    private readonly DelegateCommand _openEditGoalCommand;
    private readonly DelegateCommand _archiveGoalCommand;
    private readonly DelegateCommand _selectGoalCommand;

    private bool _isLoading;
    private bool _hasError;
    private string _errorText = string.Empty;
    private GoalListItemViewModel? _selectedGoal;
    private decimal _monthlyContribution;
    private decimal? _expectedAnnualReturnPercent;
    private decimal _projectedAmount;
    private string _selectedGoalSummary = "Выберите цель для прогноза.";

    public GoalsViewModel(IGoalService goalService, IShellState shellState, IGoalProjectionService projectionService)
    {
        _goalService = goalService;
        _shellState = shellState;
        _projectionService = projectionService;

        Goals = [];
        ProjectionSeries = [];
        AddGoalDialog = new AddGoalDialogViewModel();

        _openAddGoalCommand = new DelegateCommand(_ => OpenAddDialog());
        _openEditGoalCommand = new DelegateCommand(OpenEditDialog);
        _archiveGoalCommand = new DelegateCommand(ArchiveGoal);
        _selectGoalCommand = new DelegateCommand(SelectGoal);

        AddGoalDialog.SaveRequested += HandleSaveRequested;
        AddGoalDialog.ArchiveRequested += HandleArchiveRequested;

        LoadAsync().GetAwaiter().GetResult();
    }

    public string PageTitle => "Цели";

    public string PageDescription => "Управляйте финансовыми целями и прогнозом накоплений.";

    public ObservableCollection<GoalListItemViewModel> Goals { get; }

    public ObservableCollection<decimal> ProjectionSeries { get; }

    public AddGoalDialogViewModel AddGoalDialog { get; }

    public ICommand OpenAddGoalCommand => _openAddGoalCommand;

    public ICommand OpenEditGoalCommand => _openEditGoalCommand;

    public ICommand ArchiveGoalCommand => _archiveGoalCommand;

    public ICommand SelectGoalCommand => _selectGoalCommand;

    public decimal MonthlyContribution
    {
        get => _monthlyContribution;
        set
        {
            if (SetProperty(ref _monthlyContribution, value))
            {
                RecalculateProjection();
            }
        }
    }

    public decimal? ExpectedAnnualReturnPercent
    {
        get => _expectedAnnualReturnPercent;
        set
        {
            if (SetProperty(ref _expectedAnnualReturnPercent, value))
            {
                RecalculateProjection();
            }
        }
    }

    public decimal ProjectedAmount
    {
        get => _projectedAmount;
        private set => SetProperty(ref _projectedAmount, value);
    }

    public GoalListItemViewModel? SelectedGoal
    {
        get => _selectedGoal;
        private set
        {
            if (SetProperty(ref _selectedGoal, value))
            {
                OnPropertyChanged(nameof(HasSelectedGoal));
                MonthlyContribution = value?.MonthlyContribution ?? 0m;
                ExpectedAnnualReturnPercent = value?.ExpectedAnnualReturnPercent;
                RecalculateProjection();
            }
        }
    }

    public string SelectedGoalSummary
    {
        get => _selectedGoalSummary;
        private set => SetProperty(ref _selectedGoalSummary, value);
    }

    public bool HasSelectedGoal => SelectedGoal is not null;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasContent));
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasContent));
            }
        }
    }

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    public bool IsEmpty => !IsLoading && !HasError && Goals.Count == 0;

    public bool HasContent => !IsLoading && !HasError;

    public static GoalsViewModel CreateDesignData()
    {
        return new GoalsViewModel(new DesignGoalService(), new DesignShellState(), new GoalProjectionService());
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorText = string.Empty;

        try
        {
            IReadOnlyList<Goal> items = await _goalService.ListActiveAsync(_shellState.CurrentPortfolioId).ConfigureAwait(false);
            Goals.Clear();

            foreach (Goal goal in items)
            {
                GoalForecast forecast = _goalService.Forecast(goal, _shellState.CurrentPortfolioValue);
                decimal progressPercent = goal.TargetAmount <= 0m
                    ? 0m
                    : Math.Clamp(_shellState.CurrentPortfolioValue / goal.TargetAmount * 100m, 0m, 100m);

                Goals.Add(new GoalListItemViewModel(
                    goal.Id,
                    goal.Title,
                    goal.TargetAmount,
                    goal.Currency,
                    _shellState.CurrentPortfolioValue,
                    progressPercent,
                    goal.MonthlyContribution,
                    goal.ExpectedAnnualReturnPercent,
                    forecast));
            }

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasContent));
            SelectedGoal = Goals.FirstOrDefault();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenAddDialog()
    {
        AddGoalDialog.OpenForCreate();
    }

    private void OpenEditDialog(object? parameter)
    {
        if (parameter is GoalListItemViewModel goal)
        {
            AddGoalDialog.OpenForEdit(goal);
        }
    }

    private void ArchiveGoal(object? parameter)
    {
        if (parameter is GoalListItemViewModel goal)
        {
            _ = ArchiveGoalAsync(goal.Id);
        }
    }

    private void SelectGoal(object? parameter)
    {
        if (parameter is GoalListItemViewModel goal)
        {
            SelectedGoal = goal;
        }
    }

    private async void HandleSaveRequested(object? sender, GoalDialogSaveRequest request)
    {
        try
        {
            GoalOperationResult result = request.GoalId is null
                ? await _goalService.CreateAsync(new CreateGoalRequest(
                    _shellState.CurrentPortfolioId,
                    request.Name,
                    request.TargetAmount,
                    request.Currency,
                    request.MonthlyContribution,
                    request.ExpectedAnnualReturnPercent,
                    null)).ConfigureAwait(true)
                : await _goalService.UpdateAsync(new UpdateGoalRequest(
                    _shellState.CurrentPortfolioId,
                    request.GoalId.Value,
                    request.Name,
                    request.TargetAmount,
                    request.Currency,
                    request.MonthlyContribution,
                    request.ExpectedAnnualReturnPercent,
                    null)).ConfigureAwait(true);

            if (!result.Succeeded)
            {
                AddGoalDialog.ValidationMessage = result.Message;
                return;
            }

            AddGoalDialog.Close();
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AddGoalDialog.ValidationMessage = ex.Message;
        }
    }

    private async void HandleArchiveRequested(object? sender, Guid goalId)
    {
        await ArchiveGoalAsync(goalId).ConfigureAwait(true);
    }

    private async Task ArchiveGoalAsync(Guid goalId)
    {
        GoalOperationResult result = await _goalService.ArchiveAsync(_shellState.CurrentPortfolioId, goalId).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            AddGoalDialog.ValidationMessage = result.Message;
            return;
        }

        AddGoalDialog.Close();
        await LoadAsync().ConfigureAwait(true);
    }

    private void RecalculateProjection()
    {
        ProjectionSeries.Clear();

        if (SelectedGoal is null)
        {
            SelectedGoalSummary = "Выберите цель для прогноза.";
            ProjectedAmount = 0m;
            return;
        }

        GoalProjection projection = _projectionService.BuildProjection(new GoalProjectionRequest(
            SelectedGoal.Name,
            SelectedGoal.TargetAmount,
            SelectedGoal.CurrentAmount,
            MonthlyContribution,
            ExpectedAnnualReturnPercent));

        foreach (decimal point in projection.Series)
        {
            ProjectionSeries.Add(point);
        }

        ProjectedAmount = projection.ProjectedAmount;
        SelectedGoalSummary = projection.Summary;
    }

    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        private readonly Action<object?> _execute = execute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record GoalListItemViewModel(
    Guid Id,
    string Name,
    decimal TargetAmount,
    string Currency,
    decimal CurrentAmount,
    decimal ProgressPercent,
    decimal MonthlyContribution,
    decimal? ExpectedAnnualReturnPercent,
    GoalForecast Forecast)
{
    public string TargetText => $"{TargetAmount:N2} {Currency}";

    public string CurrentText => $"{CurrentAmount:N2} {Currency}";

    public string ProgressText => $"{ProgressPercent:0.#}%";

    public string ForecastText => Forecast.Reachable
        ? $"~ {Forecast.MonthsToGoal} мес до цели"
        : Forecast.Message;
}

public interface IGoalProjectionService
{
    GoalProjection BuildProjection(GoalProjectionRequest request);
}

public sealed class GoalProjectionService : IGoalProjectionService
{
    public GoalProjection BuildProjection(GoalProjectionRequest request)
    {
        decimal monthlyContribution = Math.Max(0m, request.MonthlyContribution);
        decimal monthlyRate = (request.ExpectedAnnualReturnPercent ?? 0m) / 100m / 12m;
        decimal value = Math.Max(0m, request.CurrentAmount);

        List<decimal> series = [value];
        int monthsToGoal = 0;

        for (int month = 1; month <= 120; month++)
        {
            value = monthlyRate == 0m
                ? value + monthlyContribution
                : value * (1m + monthlyRate) + monthlyContribution;

            series.Add(value);

            if (monthsToGoal == 0 && value >= request.TargetAmount)
            {
                monthsToGoal = month;
            }
        }

        string summary = monthsToGoal > 0
            ? $"Ориентировочно {monthsToGoal} мес до \"{request.GoalName}\"."
            : "При текущих параметрах цель не достигается за 10 лет.";

        return new GoalProjection(series, value, summary);
    }
}

public sealed record GoalProjectionRequest(
    string GoalName,
    decimal TargetAmount,
    decimal CurrentAmount,
    decimal MonthlyContribution,
    decimal? ExpectedAnnualReturnPercent);

public sealed record GoalProjection(IReadOnlyList<decimal> Series, decimal ProjectedAmount, string Summary);

file sealed class DesignShellState : IShellState
{
    public Guid CurrentPortfolioId { get; } = Guid.Parse("1df177b8-b3f6-4d80-9f0d-3027d4f4a149");

    public string CurrentPortfolioName => "Growth Portfolio";

    public decimal CurrentPortfolioValue => 25000m;
}

file sealed class DesignGoalService : IGoalService
{
    public Task<IReadOnlyList<Goal>> ListActiveAsync(Guid portfolioId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Goal> goals =
        [
            new Goal(Guid.NewGuid(), portfolioId, "Финансовая подушка", 12000m, "USD", 350m, 6m, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new Goal(Guid.NewGuid(), portfolioId, "Первый взнос на жильё", 50000m, "USD", 700m, 8m, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new Goal(Guid.NewGuid(), portfolioId, "Обучение", 18000m, "USD", 250m, 5m, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        ];

        return Task.FromResult(goals);
    }

    public Task<GoalOperationResult> CreateAsync(CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        Goal goal = new(Guid.NewGuid(), request.PortfolioId, request.Title, request.TargetAmount, request.Currency, request.MonthlyContribution, request.ExpectedAnnualReturnPercent, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return Task.FromResult(GoalOperationResult.Success(goal));
    }

    public Task<GoalOperationResult> UpdateAsync(UpdateGoalRequest request, CancellationToken cancellationToken = default)
    {
        Goal goal = new(request.GoalId, request.PortfolioId, request.Title, request.TargetAmount, request.Currency, request.MonthlyContribution, request.ExpectedAnnualReturnPercent, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return Task.FromResult(GoalOperationResult.Success(goal));
    }

    public Task<GoalOperationResult> ArchiveAsync(Guid portfolioId, Guid goalId, CancellationToken cancellationToken = default)
    {
        Goal goal = new(goalId, portfolioId, "Archived", 1m, "USD", 0m, null, null, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return Task.FromResult(GoalOperationResult.Success(goal));
    }

    public GoalForecast Forecast(Goal goal, decimal currentPortfolioValue)
    {
        return new GoalForecast(true, 18, DateTimeOffset.UtcNow.AddMonths(18), goal.TargetAmount, "OK");
    }
}
