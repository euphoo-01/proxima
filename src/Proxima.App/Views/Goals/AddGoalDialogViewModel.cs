using System.Windows.Input;
using Proxima.App.ViewModels;
using Proxima.App.Common.Commands;

namespace Proxima.App.Views.Goals;

public sealed class AddGoalDialogViewModel : ViewModelBase
{
    private readonly DelegateCommand _saveCommand;
    private readonly DelegateCommand _cancelCommand;
    private readonly DelegateCommand _archiveCommand;

    private Guid? _goalId;
    private bool _isOpen;
    private bool _isEditMode;
    private string _name = string.Empty;
    private decimal _targetAmount;
    private decimal _monthlyContribution;
    private decimal? _expectedAnnualReturnPercent = 8m;
    private string _validationMessage = string.Empty;

    public AddGoalDialogViewModel()
    {
        _saveCommand = new DelegateCommand(_ => Save());
        _cancelCommand = new DelegateCommand(_ => Close());
        _archiveCommand = new DelegateCommand(_ => Archive());
    }

    public event EventHandler<GoalDialogSaveRequest>? SaveRequested;

    public event EventHandler<Guid>? ArchiveRequested;

    public ICommand SaveCommand => _saveCommand;

    public ICommand CancelCommand => _cancelCommand;

    public ICommand ArchiveCommand => _archiveCommand;

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        private set => SetProperty(ref _isEditMode, value);
    }

    public string Title => IsEditMode ? "Редактировать цель" : "Добавить цель";

    public string ActionText => IsEditMode ? "Сохранить" : "Добавить";

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public decimal TargetAmount
    {
        get => _targetAmount;
        set => SetProperty(ref _targetAmount, value);
    }


    public decimal MonthlyContribution
    {
        get => _monthlyContribution;
        private set => SetProperty(ref _monthlyContribution, value);
    }

    public decimal? ExpectedAnnualReturnPercent
    {
        get => _expectedAnnualReturnPercent;
        private set => SetProperty(ref _expectedAnnualReturnPercent, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public void OpenForCreate(decimal monthlyContribution, decimal? expectedAnnualReturnPercent)
    {
        _goalId = null;
        IsEditMode = false;
        Name = string.Empty;
        TargetAmount = 0m;
        MonthlyContribution = monthlyContribution;
        ExpectedAnnualReturnPercent = expectedAnnualReturnPercent;
        ValidationMessage = string.Empty;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ActionText));
        IsOpen = true;
    }

    public void OpenForEdit(GoalListItemViewModel goal, decimal monthlyContribution, decimal? expectedAnnualReturnPercent)
    {
        _goalId = goal.Id;
        IsEditMode = true;
        Name = goal.Name;
        TargetAmount = goal.TargetAmount;
        MonthlyContribution = monthlyContribution;
        ExpectedAnnualReturnPercent = expectedAnnualReturnPercent;
        ValidationMessage = string.Empty;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ActionText));
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
        ValidationMessage = string.Empty;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationMessage = "Введите название цели.";
            return;
        }

        if (TargetAmount <= 0m)
        {
            ValidationMessage = "Целевая сумма должна быть больше нуля.";
            return;
        }

        ValidationMessage = string.Empty;
        SaveRequested?.Invoke(this, new GoalDialogSaveRequest(
            _goalId,
            Name.Trim(),
            TargetAmount,
            MonthlyContribution,
            ExpectedAnnualReturnPercent));
    }

    private void Archive()
    {
        if (_goalId is Guid goalId)
        {
            ArchiveRequested?.Invoke(this, goalId);
        }
    }

}

public sealed record GoalDialogSaveRequest(
    Guid? GoalId,
    string Name,
    decimal TargetAmount,
    decimal MonthlyContribution,
    decimal? ExpectedAnnualReturnPercent);
