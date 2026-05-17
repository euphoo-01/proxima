using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Proxima.App.CustomControls;
using Proxima.App.Shell;
using Proxima.App.ViewModels;
using Proxima.Core.Application.Goals;
using Proxima.Core.Domain.Goals;

namespace Proxima.App.Views.Goals;

public sealed class GoalsViewModel : ViewModelBase
{
    private const decimal DefaultMonthlyContribution = 2500m;
    private const decimal DefaultExpectedAnnualReturnPercent = HistoricalPortfolioReturnService.DefaultFallbackAnnualReturnPercent;

    private readonly IGoalService _goalService;
    private readonly IShellState _shellState;
    private readonly IGoalProjectionService _projectionService;
    private readonly IGoalProgressBaselineService _baselineService;
    private readonly IHistoricalPortfolioReturnService _historicalReturnService;
    private readonly DelegateCommand _openAddGoalCommand;
    private readonly DelegateCommand _openEditGoalCommand;
    private readonly DelegateCommand _archiveGoalCommand;
    private readonly DelegateCommand _selectHorizonCommand;

    private bool _isLoading;
    private bool _hasError;
    private string _errorText = string.Empty;
    private decimal _monthlyContribution = DefaultMonthlyContribution;
    private decimal? _expectedAnnualReturnPercent = DefaultExpectedAnnualReturnPercent;
    private decimal _currentPortfolioValue;
    private decimal _projectedAmount;
    private bool _isProjectionChartLoading;
    private string? _projectionChartErrorText;
    private int _selectedHorizonYears = 25;
    private int _currentYear = DateTimeOffset.Now.Year;
    private IReadOnlyList<GoalForecastChartPoint> _forecastPoints = [];
    private IReadOnlyList<GoalForecastMilestone> _forecastMilestones = [];
    private string _financialIndependenceYearsText = "—";
    private decimal _financialIndependenceProgressPercent;
    private string _financialIndependenceProgressText = "Прогресс: 0%";
    private string _riskLevelText = "—";
    private string _riskPillText = "Недостаточно данных";
    private string _forecastCaption = "Добавьте цель для построения прогноза.";
    private string _historicalAnnualReturnText = "—";
    private string _historicalReturnSourceText = "Доходность профиля будет рассчитана после появления истории портфеля.";
    private bool _monthlyContributionInitialized;

    public GoalsViewModel(
        IGoalService goalService,
        IShellState shellState,
        IGoalProjectionService projectionService,
        IGoalProgressBaselineService baselineService,
        IHistoricalPortfolioReturnService historicalReturnService,
        IRuntimeDataInvalidation dataInvalidation)
    {
        _goalService = goalService;
        _shellState = shellState;
        _projectionService = projectionService;
        _baselineService = baselineService;
        _historicalReturnService = historicalReturnService;

        Goals = [];
        AddGoalDialog = new AddGoalDialogViewModel();

        _openAddGoalCommand = new DelegateCommand(_ => OpenAddDialog());
        _openEditGoalCommand = new DelegateCommand(OpenEditDialog);
        _archiveGoalCommand = new DelegateCommand(ArchiveGoal);
        _selectHorizonCommand = new DelegateCommand(SelectHorizon);

        AddGoalDialog.SaveRequested += HandleSaveRequested;
        AddGoalDialog.ArchiveRequested += HandleArchiveRequested;
        _shellState.PortfolioChanged += (_, _) => _ = LoadAsync();
        dataInvalidation.DataInvalidated += (_, _) => _ = LoadAsync();

        LoadAsync().GetAwaiter().GetResult();
    }

    public string PageTitle => "Цели";

    public ObservableCollection<GoalListItemViewModel> Goals { get; }

    public AddGoalDialogViewModel AddGoalDialog { get; }

    public ICommand OpenAddGoalCommand => _openAddGoalCommand;

    public ICommand OpenEditGoalCommand => _openEditGoalCommand;

    public ICommand ArchiveGoalCommand => _archiveGoalCommand;

    public ICommand SelectHorizonCommand => _selectHorizonCommand;

    public decimal MonthlyContribution
    {
        get => _monthlyContribution;
        set
        {
            decimal normalized = Math.Max(0m, value);
            if (SetProperty(ref _monthlyContribution, normalized))
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

    public bool IsProjectionChartLoading
    {
        get => _isProjectionChartLoading;
        private set => SetProperty(ref _isProjectionChartLoading, value);
    }

    public string? ProjectionChartErrorText
    {
        get => _projectionChartErrorText;
        private set => SetProperty(ref _projectionChartErrorText, value);
    }

    public IReadOnlyList<GoalForecastChartPoint> ForecastPoints
    {
        get => _forecastPoints;
        private set => SetProperty(ref _forecastPoints, value);
    }

    public IReadOnlyList<GoalForecastMilestone> ForecastMilestones
    {
        get => _forecastMilestones;
        private set => SetProperty(ref _forecastMilestones, value);
    }

    public int SelectedHorizonYears
    {
        get => _selectedHorizonYears;
        private set
        {
            if (SetProperty(ref _selectedHorizonYears, value))
            {
                OnPropertyChanged(nameof(IsTwentyFiveYearsSelected));
                OnPropertyChanged(nameof(IsFortyYearsSelected));
                OnPropertyChanged(nameof(IsFiftyYearsSelected));
                RecalculateProjection();
            }
        }
    }

    public int CurrentYear
    {
        get => _currentYear;
        private set => SetProperty(ref _currentYear, value);
    }

    public bool IsTwentyFiveYearsSelected => SelectedHorizonYears == 25;

    public bool IsFortyYearsSelected => SelectedHorizonYears == 40;

    public bool IsFiftyYearsSelected => SelectedHorizonYears == 50;

    public string FinancialIndependenceYearsText
    {
        get => _financialIndependenceYearsText;
        private set => SetProperty(ref _financialIndependenceYearsText, value);
    }

    public decimal FinancialIndependenceProgressPercent
    {
        get => _financialIndependenceProgressPercent;
        private set => SetProperty(ref _financialIndependenceProgressPercent, Math.Clamp(value, 0m, 100m));
    }

    public string FinancialIndependenceProgressText
    {
        get => _financialIndependenceProgressText;
        private set => SetProperty(ref _financialIndependenceProgressText, value);
    }

    public string RiskLevelText
    {
        get => _riskLevelText;
        private set => SetProperty(ref _riskLevelText, value);
    }

    public string RiskPillText
    {
        get => _riskPillText;
        private set => SetProperty(ref _riskPillText, value);
    }

    public string ForecastCaption
    {
        get => _forecastCaption;
        private set => SetProperty(ref _forecastCaption, value);
    }

    public string HistoricalAnnualReturnText
    {
        get => _historicalAnnualReturnText;
        private set => SetProperty(ref _historicalAnnualReturnText, value);
    }

    public string HistoricalReturnSourceText
    {
        get => _historicalReturnSourceText;
        private set => SetProperty(ref _historicalReturnSourceText, value);
    }

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

private async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorText = string.Empty;
        CurrentYear = DateTimeOffset.Now.Year;

        try
        {
            IReadOnlyList<Goal> items = await _goalService.ListActiveAsync(_shellState.CurrentPortfolioId).ConfigureAwait(false);
            HistoricalPortfolioReturn historicalReturn = await _historicalReturnService
                .CalculateAsync(_shellState.CurrentPortfolioId)
                .ConfigureAwait(false);

            Goals.Clear();
            ApplyHistoricalReturn(historicalReturn);

            IReadOnlyDictionary<Guid, decimal> baselineByGoal = _baselineService.CalculateCurrentAmounts(_shellState.CurrentPortfolioId, items);
            _currentPortfolioValue = ResolveCurrentPortfolioValue(baselineByGoal);
            InitializeMonthlyContribution(items);

            foreach (Goal goal in items.OrderBy(item => item.TargetAmount).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase))
            {
                decimal currentAmount = Math.Min(goal.TargetAmount, Math.Max(0m, _currentPortfolioValue));
                decimal progressPercent = goal.TargetAmount <= 0m
                    ? 0m
                    : Math.Clamp(currentAmount / goal.TargetAmount * 100m, 0m, 100m);

                GoalMilestoneEstimate estimate = _projectionService.EstimateGoalReach(new GoalReachEstimateRequest(
                    _currentPortfolioValue,
                    MonthlyContribution,
                    ExpectedAnnualReturnPercent,
                    goal.TargetAmount,
                    SelectedHorizonYears,
                    CurrentYear));

                Goals.Add(new GoalListItemViewModel(
                    goal.Id,
                    goal.Title,
                    goal.TargetAmount,
                    goal.Currency,
                    currentAmount,
                    progressPercent,
                    goal.MonthlyContribution,
                    goal.ExpectedAnnualReturnPercent,
                    estimate));
            }

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasContent));
            RecalculateProjection();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = ex.Message;
            ForecastPoints = [];
            ForecastMilestones = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private decimal ResolveCurrentPortfolioValue(IReadOnlyDictionary<Guid, decimal> baselineByGoal)
    {
        decimal derived = baselineByGoal.Values.Sum();
        if (derived > 0m)
        {
            return derived;
        }

        return Math.Max(0m, _shellState.CurrentPortfolioValue);
    }

    private void InitializeMonthlyContribution(IReadOnlyList<Goal> goals)
    {
        if (_monthlyContributionInitialized)
        {
            return;
        }

        decimal storedMonthlyContribution = goals
            .Select(goal => Math.Max(0m, goal.MonthlyContribution))
            .DefaultIfEmpty(0m)
            .Max();

        _monthlyContribution = storedMonthlyContribution > 0m
            ? storedMonthlyContribution
            : DefaultMonthlyContribution;

        _monthlyContributionInitialized = true;
        OnPropertyChanged(nameof(MonthlyContribution));
    }

    private void ApplyHistoricalReturn(HistoricalPortfolioReturn historicalReturn)
    {
        _expectedAnnualReturnPercent = historicalReturn.AnnualizedReturnPercent ?? DefaultExpectedAnnualReturnPercent;
        HistoricalAnnualReturnText = FormatAnnualReturn(_expectedAnnualReturnPercent, historicalReturn.IsFallback);
        HistoricalReturnSourceText = historicalReturn.Message;
        OnPropertyChanged(nameof(ExpectedAnnualReturnPercent));
    }

    private static string FormatAnnualReturn(decimal? annualReturnPercent, bool isFallback)
    {
        if (annualReturnPercent is not decimal value)
        {
            return "—";
        }

        string suffix = isFallback ? " / год" : "% / год";
        string prefix = value > 0m ? "+" : string.Empty;

        return isFallback
            ? $"{prefix}{value:0.#}% / год"
            : $"{prefix}{value:0.#}{suffix}";
    }

    private void OpenAddDialog()
    {
        AddGoalDialog.OpenForCreate(MonthlyContribution, ExpectedAnnualReturnPercent);
    }

    private void OpenEditDialog(object? parameter)
    {
        if (parameter is GoalListItemViewModel goal)
        {
            AddGoalDialog.OpenForEdit(goal, MonthlyContribution, ExpectedAnnualReturnPercent);
        }
    }

    private void ArchiveGoal(object? parameter)
    {
        if (parameter is GoalListItemViewModel goal)
        {
            _ = ArchiveGoalAsync(goal.Id);
        }
    }

    private void SelectHorizon(object? parameter)
    {
        if (parameter is int years)
        {
            SelectedHorizonYears = years;
            return;
        }

        if (parameter is string raw && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            SelectedHorizonYears = parsed;
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
                    MonthlyContribution,
                    ExpectedAnnualReturnPercent,
                    null)).ConfigureAwait(true)
                : await _goalService.UpdateAsync(new UpdateGoalRequest(
                    _shellState.CurrentPortfolioId,
                    request.GoalId.Value,
                    request.Name,
                    request.TargetAmount,
                    MonthlyContribution,
                    ExpectedAnnualReturnPercent,
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
        IsProjectionChartLoading = true;
        ProjectionChartErrorText = null;

        try
        {
            if (Goals.Count == 0)
            {
                ForecastPoints = [];
                ForecastMilestones = [];
                ProjectedAmount = 0m;
                FinancialIndependenceYearsText = "—";
                FinancialIndependenceProgressPercent = 0m;
                FinancialIndependenceProgressText = "Прогресс: 0%";
                RiskLevelText = "—";
                RiskPillText = "Нет целей";
                ForecastCaption = "Добавьте цель, чтобы увидеть точки достижения на графике.";
                return;
            }

            GoalProjection projection = _projectionService.BuildProjection(new GoalProjectionRequest(
                _currentPortfolioValue,
                MonthlyContribution,
                ExpectedAnnualReturnPercent,
                SelectedHorizonYears,
                CurrentYear,
                Goals.Select(goal => new GoalProjectionTarget(goal.Id, goal.Name, goal.Currency, goal.TargetAmount)).ToArray()));

            ForecastPoints = projection.Points;
            ForecastMilestones = BuildChartMilestones(projection);
            ProjectedAmount = projection.ProjectedAmount;

            UpdateGoalReachEstimates(projection);
            UpdateSummaryCards(projection);
        }
        catch (Exception ex)
        {
            ForecastPoints = [];
            ForecastMilestones = [];
            ProjectedAmount = 0m;
            ProjectionChartErrorText = ex.Message;
            RiskLevelText = "Ошибка";
            RiskPillText = "Проверьте параметры";
        }
        finally
        {
            IsProjectionChartLoading = false;
        }
    }

    private IReadOnlyList<GoalForecastMilestone> BuildChartMilestones(GoalProjection projection)
    {
        Dictionary<Guid, GoalForecastMilestone> milestoneByGoal = projection.Milestones
            .GroupBy(item => item.GoalId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.MonthIndex).First());

        return Goals
            .Select(goal => milestoneByGoal.TryGetValue(goal.Id, out GoalForecastMilestone? milestone) ? milestone : null)
            .Where(milestone => milestone is not null)
            .Select(milestone => milestone!)
            .OrderBy(milestone => milestone.MonthIndex)
            .ThenBy(milestone => milestone.TargetAmount)
            .ThenBy(milestone => milestone.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private void UpdateGoalReachEstimates(GoalProjection projection)
    {
        Dictionary<Guid, GoalForecastMilestone> milestoneByGoal = projection.Milestones.ToDictionary(item => item.GoalId);
        for (int i = 0; i < Goals.Count; i++)
        {
            GoalListItemViewModel goal = Goals[i];
            GoalMilestoneEstimate estimate = milestoneByGoal.TryGetValue(goal.Id, out GoalForecastMilestone? milestone)
                ? GoalMilestoneEstimate.Reachable(milestone.MonthIndex, milestone.EstimatedYear)
                : GoalMilestoneEstimate.Unreachable(SelectedHorizonYears);

            Goals[i] = goal with { Estimate = estimate };
        }
    }

    private void UpdateSummaryCards(GoalProjection projection)
    {
        decimal maxTarget = Goals.Max(goal => goal.TargetAmount);
        decimal progress = maxTarget <= 0m ? 0m : _currentPortfolioValue / maxTarget * 100m;
        FinancialIndependenceProgressPercent = progress;
        FinancialIndependenceProgressText = $"Прогресс: {Math.Clamp(progress, 0m, 100m):0.#}%";

        int totalGoals = Goals.Count;
        int reachedGoals = projection.Milestones.Count;
        GoalForecastMilestone? farthestMilestone = projection.Milestones.OrderByDescending(item => item.MonthIndex).FirstOrDefault();

        FinancialIndependenceYearsText = farthestMilestone is null
            ? $"> {SelectedHorizonYears} лет"
            : $"{Math.Max(1, (int)Math.Ceiling(farthestMilestone.MonthIndex / 12d))} лет";

        if (reachedGoals == totalGoals)
        {
            RiskLevelText = "Низкий";
            RiskPillText = "Сбалансирован";
        }
        else if (reachedGoals > 0)
        {
            RiskLevelText = "Средний";
            RiskPillText = "Требует взноса";
        }
        else
        {
            RiskLevelText = "Высокий";
            RiskPillText = "Цели вне горизонта";
        }

        ForecastCaption = reachedGoals == 0
            ? $"При текущем взносе цели не достигаются за {SelectedHorizonYears} лет."
            : $"{reachedGoals} из {totalGoals} целей достигаются в выбранном горизонте.";
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
    GoalMilestoneEstimate Estimate)
{
    public string TargetText => FormatMoney(TargetAmount, Currency);

    public string CurrentText => FormatMoney(CurrentAmount, Currency);

    public string ProgressText => $"{ProgressPercent:0.#}%";

    public string ForecastText => Estimate.IsReachable
        ? $"Достигнете в {Estimate.EstimatedYear}"
        : $"> {Estimate.HorizonYears} лет";

    public string ThumbnailText => string.IsNullOrWhiteSpace(Name)
        ? "Ц"
        : Name.Trim()[0].ToString().ToUpper(CultureInfo.CurrentCulture);

    private static string FormatMoney(decimal value, string currency)
    {
        string prefix = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ? "$" : string.Empty;
        string suffix = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ? string.Empty : $" {currency}";
        return $"{prefix}{value:N0}{suffix}";
    }
}

public interface IGoalProjectionService
{
    GoalProjection BuildProjection(GoalProjectionRequest request);

    GoalMilestoneEstimate EstimateGoalReach(GoalReachEstimateRequest request);
}

public sealed class GoalProjectionService : IGoalProjectionService
{
    private const decimal DefaultExpectedAnnualReturnPercent = HistoricalPortfolioReturnService.DefaultFallbackAnnualReturnPercent;

    public GoalProjection BuildProjection(GoalProjectionRequest request)
    {
        decimal monthlyContribution = Math.Max(0m, request.MonthlyContribution);
        decimal monthlyRate = NormalizeMonthlyRate(request.ExpectedAnnualReturnPercent);
        decimal value = Math.Max(0m, request.CurrentAmount);
        int totalMonths = Math.Max(1, request.HorizonYears * 12);

        List<GoalForecastChartPoint> points = new(totalMonths + 1);
        List<GoalForecastMilestone> milestones = [];
        HashSet<Guid> reachedGoalIds = [];

        for (int month = 0; month <= totalMonths; month++)
        {
            if (month > 0)
            {
                value = value * (1m + monthlyRate) + monthlyContribution;
            }

            points.Add(new GoalForecastChartPoint(month, value));

            foreach (GoalProjectionTarget target in request.Targets.OrderBy(item => item.TargetAmount))
            {
                if (reachedGoalIds.Contains(target.GoalId) || value < target.TargetAmount)
                {
                    continue;
                }

                reachedGoalIds.Add(target.GoalId);
                milestones.Add(new GoalForecastMilestone(
                    target.GoalId,
                    target.Title,
                    target.Currency,
                    target.TargetAmount,
                    month,
                    DateTimeOffset.Now.AddMonths(month).Year));
            }
        }

        return new GoalProjection(points, milestones.OrderBy(item => item.MonthIndex).ToArray(), value);
    }

    public GoalMilestoneEstimate EstimateGoalReach(GoalReachEstimateRequest request)
    {
        decimal monthlyContribution = Math.Max(0m, request.MonthlyContribution);
        decimal monthlyRate = NormalizeMonthlyRate(request.ExpectedAnnualReturnPercent);
        decimal value = Math.Max(0m, request.CurrentAmount);
        int totalMonths = Math.Max(1, request.HorizonYears * 12);

        if (value >= request.TargetAmount)
        {
            return GoalMilestoneEstimate.Reachable(0, request.StartYear);
        }

        for (int month = 1; month <= totalMonths; month++)
        {
            value = value * (1m + monthlyRate) + monthlyContribution;
            if (value >= request.TargetAmount)
            {
                return GoalMilestoneEstimate.Reachable(month, DateTimeOffset.Now.AddMonths(month).Year);
            }
        }

        return GoalMilestoneEstimate.Unreachable(request.HorizonYears);
    }

    private static decimal NormalizeMonthlyRate(decimal? expectedAnnualReturnPercent)
    {
        decimal annualPercent = Math.Clamp(expectedAnnualReturnPercent ?? DefaultExpectedAnnualReturnPercent, -50m, 100m);
        double annualRate = (double)annualPercent / 100d;
        double monthlyRate = Math.Pow(1d + annualRate, 1d / 12d) - 1d;
        return (decimal)monthlyRate;
    }
}

public sealed record GoalProjectionRequest(
    decimal CurrentAmount,
    decimal MonthlyContribution,
    decimal? ExpectedAnnualReturnPercent,
    int HorizonYears,
    int StartYear,
    IReadOnlyList<GoalProjectionTarget> Targets);

public sealed record GoalProjectionTarget(Guid GoalId, string Title, string Currency, decimal TargetAmount);

public sealed record GoalProjection(
    IReadOnlyList<GoalForecastChartPoint> Points,
    IReadOnlyList<GoalForecastMilestone> Milestones,
    decimal ProjectedAmount);

public sealed record GoalReachEstimateRequest(
    decimal CurrentAmount,
    decimal MonthlyContribution,
    decimal? ExpectedAnnualReturnPercent,
    decimal TargetAmount,
    int HorizonYears,
    int StartYear);

public sealed record GoalMilestoneEstimate(bool IsReachable, int MonthsToGoal, int? EstimatedYear, int HorizonYears)
{
    public static GoalMilestoneEstimate Reachable(int monthsToGoal, int estimatedYear)
    {
        return new GoalMilestoneEstimate(true, monthsToGoal, estimatedYear, 0);
    }

    public static GoalMilestoneEstimate Unreachable(int horizonYears)
    {
        return new GoalMilestoneEstimate(false, 0, null, horizonYears);
    }
}

