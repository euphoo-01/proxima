using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Proxima.App.Shell;
using Proxima.App.Notifications;
using Proxima.App.ViewModels;
using Proxima.App.Auth;
using Proxima.Core.Application.Auth;
using Proxima.Core.Application.Portfolios;
using Proxima.Core.Application.Settings;
using Proxima.Core.Domain.Auth;
using Proxima.Core.Domain.Portfolios;
using Proxima.App.Common.Commands;

namespace Proxima.App.Views.Profile;

public sealed class ProfileViewModel : ViewModelBase, IDisposable
{
    private readonly IRuntimeUserContext _userContext;
    private readonly ISettingsService _settingsService;
    private readonly ILocalUserRepository _localUsers;
    private readonly IPortfolioService _portfolioService;
    private readonly ILocalAuthService _localAuthService;
    private readonly IShellState _shellState;
    private readonly IShellPortfolioCoordinator _portfolioCoordinator;
    private readonly IRuntimeDataInvalidation _runtimeDataInvalidation;
    private readonly IAppNotificationCenter _notificationCenter;

    private readonly AsyncCommand _saveCommand;
    private readonly AsyncCommand _reloadCommand;
    private readonly AsyncCommand _addPortfolioCommand;
    private readonly AsyncCommand _archivePortfolioCommand;
    private readonly AsyncParameterCommand _savePortfolioCommand;
    private readonly AsyncCommand _saveTwelveDataKeyCommand;
    private readonly AsyncCommand _deleteAccountCommand;

    private string _displayName = string.Empty;
    private string _login = string.Empty;
    private string _location = "Минск, Беларусь";
    private UserRole _selectedRole = UserRole.PrivateInvestor;
    private LegalProfileKind _selectedLegalProfile = LegalProfileKind.PhysicalPerson;
    private Bitmap? _avatarBitmap;
    private bool _isLoading;
    private bool _isSaving;
    private bool _isApiPanelOpen;
    private bool _deleteConfirmationPending;
    private string _twelveDataApiKey = string.Empty;
    private string _twelveDataKeyStatus = "Ключ Twelve Data не задан.";
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;

    public ProfileViewModel(
        IRuntimeUserContext userContext,
        ISettingsService settingsService,
        ILocalUserRepository localUsers,
        IPortfolioService portfolioService,
        ILocalAuthService localAuthService,
        IShellState shellState,
        IShellPortfolioCoordinator portfolioCoordinator,
        IRuntimeDataInvalidation runtimeDataInvalidation,
        IAppNotificationCenter notificationCenter)
    {
        _userContext = userContext;
        _settingsService = settingsService;
        _localUsers = localUsers;
        _portfolioService = portfolioService;
        _localAuthService = localAuthService;
        _shellState = shellState;
        _portfolioCoordinator = portfolioCoordinator;
        _runtimeDataInvalidation = runtimeDataInvalidation;
        _notificationCenter = notificationCenter;

        _saveCommand = new AsyncCommand(SaveAsync, () => !IsBusy);
        _reloadCommand = new AsyncCommand(LoadAsync, () => !IsBusy);
        _addPortfolioCommand = new AsyncCommand(AddPortfolioAsync, () => IsFinancialConsultant && !IsBusy);
        _archivePortfolioCommand = new AsyncCommand(ArchivePortfolioAsync, () => IsFinancialConsultant && !IsBusy);
        _savePortfolioCommand = new AsyncParameterCommand(SavePortfolioAsync, parameter => parameter is ProfilePortfolioItem && !IsBusy);
        _saveTwelveDataKeyCommand = new AsyncCommand(SaveTwelveDataKeyAsync, () => !IsBusy);
        _deleteAccountCommand = new AsyncCommand(DeleteAccountAsync, () => !IsBusy);

        SelectPrivateInvestorCommand = new DelegateCommand(_ => SelectedRole = UserRole.PrivateInvestor);
        SelectFinancialConsultantCommand = new DelegateCommand(_ => SelectedRole = UserRole.FinancialAnalyst);
        SelectPhysicalPersonCommand = new DelegateCommand(_ => SelectedLegalProfile = LegalProfileKind.PhysicalPerson);
        SelectSelfEmployedCommand = new DelegateCommand(_ => SelectedLegalProfile = LegalProfileKind.SelfEmployed);
        SelectSoleProprietorCommand = new DelegateCommand(_ => SelectedLegalProfile = LegalProfileKind.SoleProprietor);
        SelectCompanyCommand = new DelegateCommand(_ => SelectedLegalProfile = LegalProfileKind.Company);
        SelectPortfolioCommand = new DelegateCommand(SelectPortfolioFromCommand, parameter => parameter is ProfilePortfolioItem);
        ToggleApiPanelCommand = new DelegateCommand(_ => IsApiPanelOpen = !IsApiPanelOpen);

        Portfolios = [];

        _userContext.ProfileChanged += HandleProfileChanged;
        _runtimeDataInvalidation.DataInvalidated += HandleDataInvalidated;
        _shellState.PortfolioChanged += HandlePortfolioChanged;

        _ = LoadAsync();
    }

    public void Dispose()
    {
        _userContext.ProfileChanged -= HandleProfileChanged;
        _runtimeDataInvalidation.DataInvalidated -= HandleDataInvalidated;
        _shellState.PortfolioChanged -= HandlePortfolioChanged;
        AvatarBitmap = null;
    }

    private void HandleProfileChanged(object? sender, EventArgs e)
    {
        RefreshComputedProfileProperties();
    }

    private void HandleDataInvalidated(object? sender, RuntimeDataInvalidatedEventArgs args)
    {
        if (args.Reason.Contains("portfolio", StringComparison.OrdinalIgnoreCase))
        {
            _ = ReloadPortfoliosAsync();
        }
    }

    private void HandlePortfolioChanged(object? sender, ShellPortfolioChangedEventArgs args)
    {
        MarkSelectedPortfolio(args.PortfolioId);
    }

    public ObservableCollection<ProfilePortfolioItem> Portfolios { get; }

    public ICommand SaveCommand => _saveCommand;

    public ICommand ReloadCommand => _reloadCommand;

    public ICommand AddPortfolioCommand => _addPortfolioCommand;

    public ICommand ArchivePortfolioCommand => _archivePortfolioCommand;

    public ICommand SavePortfolioCommand => _savePortfolioCommand;

    public ICommand SaveTwelveDataKeyCommand => _saveTwelveDataKeyCommand;

    public ICommand DeleteAccountCommand => _deleteAccountCommand;

    public ICommand SelectPrivateInvestorCommand { get; }

    public ICommand SelectFinancialConsultantCommand { get; }

    public ICommand SelectPhysicalPersonCommand { get; }

    public ICommand SelectSelfEmployedCommand { get; }

    public ICommand SelectSoleProprietorCommand { get; }

    public ICommand SelectCompanyCommand { get; }

    public ICommand SelectPortfolioCommand { get; }

    public ICommand ToggleApiPanelCommand { get; }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
            {
                OnPropertyChanged(nameof(Initial));
            }
        }
    }

    public string Login
    {
        get => _login;
        private set => SetProperty(ref _login, value);
    }

    public string Location
    {
        get => _location;
        set => SetProperty(ref _location, value);
    }

    public UserRole SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (SetProperty(ref _selectedRole, value))
            {
                _deleteConfirmationPending = false;
                RefreshRoleVisibility();
            }
        }
    }

    public LegalProfileKind SelectedLegalProfile
    {
        get => _selectedLegalProfile;
        set
        {
            if (SetProperty(ref _selectedLegalProfile, value))
            {
                OnPropertyChanged(nameof(IsPhysicalPersonSelected));
                OnPropertyChanged(nameof(IsSelfEmployedSelected));
                OnPropertyChanged(nameof(IsSoleProprietorSelected));
                OnPropertyChanged(nameof(IsCompanySelected));
            }
        }
    }

    public Bitmap? AvatarBitmap
    {
        get => _avatarBitmap;
        private set
        {
            if (ReferenceEquals(_avatarBitmap, value))
            {
                return;
            }

            Bitmap? old = _avatarBitmap;
            _avatarBitmap = value;
            old?.Dispose();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasAvatar));
            OnPropertyChanged(nameof(ShowInitialAvatar));
        }
    }

    public bool HasAvatar => AvatarBitmap is not null;

    public bool ShowInitialAvatar => !HasAvatar;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshBusyState();
            }
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value))
            {
                RefreshBusyState();
            }
        }
    }

    public bool IsApiPanelOpen
    {
        get => _isApiPanelOpen;
        set => SetProperty(ref _isApiPanelOpen, value);
    }

    public string TwelveDataApiKey
    {
        get => _twelveDataApiKey;
        set => SetProperty(ref _twelveDataApiKey, value);
    }

    public string TwelveDataKeyStatus
    {
        get => _twelveDataKeyStatus;
        private set => SetProperty(ref _twelveDataKeyStatus, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
                NotifyIfFinalStatus(value);
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _ = _notificationCenter.NotifyAsync(AppNotificationLevel.Error, "Ошибка профиля", value, "Профиль");
                }
            }
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsBusy => IsLoading || IsSaving;

    public string Initial => string.IsNullOrWhiteSpace(DisplayName)
        ? "П"
        : DisplayName.Trim()[..1].ToUpperInvariant();

    public string RoleDisplayName => SelectedRole.ToDisplayName();

    public bool IsPrivateInvestor => SelectedRole == UserRole.PrivateInvestor;

    public bool IsFinancialConsultant => SelectedRole == UserRole.FinancialAnalyst;

    public bool IsPrivateInvestorSelected => IsPrivateInvestor;

    public bool IsFinancialConsultantSelected => IsFinancialConsultant;

    public bool ShowPortfolioManagement => IsFinancialConsultant;

    public bool HidePortfolioManagement => !ShowPortfolioManagement;

    public bool IsUsdSelected => true;

    public bool IsBynSelected => false;

    public bool IsPhysicalPersonSelected => SelectedLegalProfile == LegalProfileKind.PhysicalPerson;

    public bool IsSelfEmployedSelected => SelectedLegalProfile == LegalProfileKind.SelfEmployed;

    public bool IsSoleProprietorSelected => SelectedLegalProfile == LegalProfileKind.SoleProprietor;

    public bool IsCompanySelected => SelectedLegalProfile == LegalProfileKind.Company;

    public string DeleteAccountButtonText => _deleteConfirmationPending
        ? "Подтвердить удаление"
        : "Удалить аккаунт";

    public async Task ChangeAvatarAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        ErrorMessage = string.Empty;

        if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty)
        {
            ErrorMessage = "Пользователь не авторизован.";
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            ErrorMessage = "Файл аватара не найден.";
            return;
        }

        try
        {
            Directory.CreateDirectory(GetProfileExtrasDirectory());
            string targetPath = GetAvatarPath(_userContext.UserId);

            await using (FileStream input = File.OpenRead(sourcePath))
            await using (FileStream output = File.Create(targetPath))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(true);
            }

            LoadAvatarFromDisk();
            _userContext.UpdateRuntimeProfile(DisplayName, SelectedRole);
            StatusMessage = "Аватар обновлен.";
        }
        catch (Exception ex)
        {
            ErrorMessage = BuildErrorMessage(ex);
        }
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        try
        {
            if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty)
            {
                ErrorMessage = "Пользователь не авторизован.";
                return;
            }

            UserSettings settings = await _settingsService.EnsureAsync(new CreateDefaultSettingsRequest(
                _userContext.UserId)).ConfigureAwait(true);

            DisplayName = _userContext.DisplayName;
            Login = _userContext.Login;
            SelectedRole = _userContext.Role;
            LoadProfileExtrasFromDisk();
            LoadAvatarFromDisk();
            TwelveDataKeyStatus = string.IsNullOrWhiteSpace(settings.QuoteApiKey)
                ? "Ключ Twelve Data не задан."
                : "Ключ Twelve Data сохранен.";

            _userContext.UpdateRuntimeProfile(DisplayName, SelectedRole);

            await ReloadPortfoliosAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = BuildErrorMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        IsSaving = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Сохранение профиля...";

        try
        {
            if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty)
            {
                ErrorMessage = "Пользователь не авторизован.";
                return;
            }

            LocalUserProfile? currentProfile = await _localUsers
                .FindByLoginAsync(_userContext.Login, CancellationToken.None)
                .ConfigureAwait(true);

            if (currentProfile is null || currentProfile.Id != _userContext.UserId)
            {
                ErrorMessage = "Не удалось найти текущий локальный профиль.";
                return;
            }

            string normalizedDisplayName = NormalizeDisplayName(DisplayName);
            await _localUsers.UpdateAsync(currentProfile with
            {
                DisplayName = normalizedDisplayName,
                Role = SelectedRole,
                UpdatedAt = DateTimeOffset.UtcNow,
            }, CancellationToken.None).ConfigureAwait(true);

            UserSettings settings = await _settingsService.EnsureAsync(new CreateDefaultSettingsRequest(
                _userContext.UserId)).ConfigureAwait(true);

            SaveProfileExtrasToDisk();
            await SaveDirtyPortfoliosAsync().ConfigureAwait(true);

            DisplayName = normalizedDisplayName;
            TwelveDataKeyStatus = string.IsNullOrWhiteSpace(settings.QuoteApiKey)
                ? "Ключ Twelve Data не задан."
                : "Ключ Twelve Data сохранен.";

            _userContext.UpdateRuntimeProfile(DisplayName, SelectedRole);

            await ReloadPortfoliosAsync().ConfigureAwait(true);
            _runtimeDataInvalidation.Invalidate("profile-saved-portfolio-refresh");

            StatusMessage = "Профиль сохранен.";
        }
        catch (Exception ex)
        {
            ErrorMessage = BuildErrorMessage(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task SaveTwelveDataKeyAsync()
    {
        IsSaving = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Сохранение API ключа...";

        try
        {
            if (string.IsNullOrWhiteSpace(TwelveDataApiKey))
            {
                ErrorMessage = "Вставьте Twelve Data API key.";
                return;
            }

            UserSettings current = await _settingsService.EnsureAsync(new CreateDefaultSettingsRequest(
                _userContext.UserId)).ConfigureAwait(true);

            SettingsOperationResult result = await _settingsService.UpdateAsync(new UpdateSettingsRequest(
                _userContext.UserId,
                QuoteProviderKind.TwelveData,
                TwelveDataApiKey,
                current.CurrencyProvider)).ConfigureAwait(true);

            if (!result.Succeeded || result.Settings is null)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "Не удалось сохранить Twelve Data API key."
                    : result.Message;

                return;
            }

            TwelveDataApiKey = string.Empty;
            TwelveDataKeyStatus = "Ключ Twelve Data сохранен. Провайдер котировок переключен на Twelve Data.";
            _runtimeDataInvalidation.Invalidate("twelve-data-api-key-saved");
            StatusMessage = "API ключ сохранен.";
        }
        catch (Exception ex)
        {
            ErrorMessage = BuildErrorMessage(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task DeleteAccountAsync()
    {
        if (!_deleteConfirmationPending)
        {
            _deleteConfirmationPending = true;
            StatusMessage = "Нажмите «Подтвердить удаление», чтобы удалить локальный профиль и связанные данные из БД.";
            OnPropertyChanged(nameof(DeleteAccountButtonText));
            return;
        }

        IsSaving = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Удаление аккаунта...";

        try
        {
            Guid userId = _userContext.UserId;
            if (userId == Guid.Empty)
            {
                ErrorMessage = "Пользователь не авторизован.";
                return;
            }

            await _localAuthService.DeleteProfileAsync(userId).ConfigureAwait(true);
            DeleteProfileExtras(userId);
            _userContext.ClearAuthentication();

            StatusMessage = "Аккаунт удален. Открываю экран регистрации...";
        }
        catch (Exception ex)
        {
            ErrorMessage = BuildErrorMessage(ex);
        }
        finally
        {
            _deleteConfirmationPending = false;
            OnPropertyChanged(nameof(DeleteAccountButtonText));
            IsSaving = false;
        }
    }

    private async Task ReloadPortfoliosAsync()
    {
        Portfolios.Clear();

        if (!IsFinancialConsultant || _userContext.UserId == Guid.Empty)
        {
            return;
        }

        IReadOnlyList<Portfolio> portfolios = await _portfolioService
            .ListActiveAsync(_userContext.UserId)
            .ConfigureAwait(true);

        foreach (Portfolio portfolio in portfolios.OrderBy(static x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            ProfilePortfolioItem item = new(
                portfolio.Id,
                NormalizePortfolioName(portfolio.Name),
                string.IsNullOrWhiteSpace(portfolio.Description) ? "Клиентский портфель" : portfolio.Description)
            {
                IsSelected = portfolio.Id == _shellState.CurrentPortfolioId,
            };

            Portfolios.Add(item);
        }

        bool currentPortfolioFound = Portfolios.Any(x => x.Id == _shellState.CurrentPortfolioId);
        ProfilePortfolioItem? selected = Portfolios.FirstOrDefault(x => x.IsSelected) ?? Portfolios.FirstOrDefault();
        if (selected is not null)
        {
            SelectPortfolio(selected, updateShell: !currentPortfolioFound);
        }
    }

    private async Task AddPortfolioAsync()
    {
        if (!IsFinancialConsultant || _userContext.UserId == Guid.Empty)
        {
            return;
        }

        IsSaving = true;
        ErrorMessage = string.Empty;

        try
        {
            await _settingsService.EnsureAsync(new CreateDefaultSettingsRequest(
                _userContext.UserId)).ConfigureAwait(true);

            int index = Portfolios.Count + 1;
            string name = $"Портфель {index}";

            while (Portfolios.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                index++;
                name = $"Портфель {index}";
            }

            PortfolioOperationResult result = await _portfolioService.CreateAsync(new CreatePortfolioRequest(
                _userContext.UserId,
                name,
                "Клиентский портфель",
                null)).ConfigureAwait(true);

            if (!result.Succeeded || result.Portfolio is null)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "Не удалось создать портфель."
                    : result.Message;

                return;
            }

            _portfolioCoordinator.SetCurrentPortfolio(result.Portfolio.Id, NormalizePortfolioName(result.Portfolio.Name), _shellState.CurrentPortfolioValue);
            await ReloadPortfoliosAsync().ConfigureAwait(true);
            _runtimeDataInvalidation.Invalidate("portfolio-created");
            StatusMessage = "Портфель создан. Название можно изменить прямо в списке.";
        }
        catch (Exception ex)
        {
            ErrorMessage = BuildErrorMessage(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task ArchivePortfolioAsync()
    {
        if (!IsFinancialConsultant || _userContext.UserId == Guid.Empty)
        {
            return;
        }

        ProfilePortfolioItem? portfolio = Portfolios.FirstOrDefault(x => x.IsSelected) ?? Portfolios.LastOrDefault();
        if (portfolio is null)
        {
            StatusMessage = "Нет портфеля для удаления.";
            return;
        }

        IsSaving = true;
        ErrorMessage = string.Empty;

        try
        {
            PortfolioOperationResult result = await _portfolioService
                .ArchiveAsync(_userContext.UserId, portfolio.Id)
                .ConfigureAwait(true);

            if (!result.Succeeded)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "Не удалось удалить портфель."
                    : result.Message;

                return;
            }

            await ReloadPortfoliosAsync().ConfigureAwait(true);
            ProfilePortfolioItem? next = Portfolios.FirstOrDefault();
            if (next is not null)
            {
                SelectPortfolio(next, updateShell: true);
            }

            _runtimeDataInvalidation.Invalidate("portfolio-archived");
            StatusMessage = "Портфель удален.";
        }
        catch (Exception ex)
        {
            ErrorMessage = BuildErrorMessage(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task SavePortfolioAsync(object? parameter)
    {
        if (parameter is not ProfilePortfolioItem item)
        {
            return;
        }

        IsSaving = true;
        ErrorMessage = string.Empty;

        try
        {
            bool saved = await SavePortfolioItemAsync(item).ConfigureAwait(true);
            if (!saved)
            {
                return;
            }

            await ReloadPortfoliosAsync().ConfigureAwait(true);
            _runtimeDataInvalidation.Invalidate("portfolio-renamed");
            StatusMessage = "Название портфеля сохранено.";
        }
        catch (Exception ex)
        {
            ErrorMessage = BuildErrorMessage(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task SaveDirtyPortfoliosAsync()
    {
        foreach (ProfilePortfolioItem item in Portfolios.Where(static x => x.IsDirty).ToArray())
        {
            bool saved = await SavePortfolioItemAsync(item).ConfigureAwait(true);
            if (!saved)
            {
                return;
            }
        }
    }

    private async Task<bool> SavePortfolioItemAsync(ProfilePortfolioItem item)
    {
        if (_userContext.UserId == Guid.Empty)
        {
            ErrorMessage = "Пользователь не авторизован.";
            return false;
        }

        string normalizedName = NormalizePortfolioName(item.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            ErrorMessage = "Название портфеля обязательно.";
            return false;
        }

        PortfolioOperationResult result = await _portfolioService.UpdateAsync(new UpdatePortfolioRequest(
            _userContext.UserId,
            item.Id,
            normalizedName,
            item.Description,
            null)).ConfigureAwait(true);

        if (!result.Succeeded || result.Portfolio is null)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                ? "Не удалось сохранить портфель."
                : result.Message;

            return false;
        }

        item.AcceptChanges(NormalizePortfolioName(result.Portfolio.Name), result.Portfolio.Description ?? "Клиентский портфель");

        if (item.IsSelected || result.Portfolio.Id == _shellState.CurrentPortfolioId)
        {
            _portfolioCoordinator.SetCurrentPortfolio(result.Portfolio.Id, NormalizePortfolioName(result.Portfolio.Name), _shellState.CurrentPortfolioValue);
        }

        return true;
    }

    private void SelectPortfolioFromCommand(object? parameter)
    {
        if (parameter is ProfilePortfolioItem item)
        {
            SelectPortfolio(item, updateShell: true);
        }
    }

    private void SelectPortfolio(ProfilePortfolioItem item, bool updateShell)
    {
        MarkSelectedPortfolio(item.Id);

        if (updateShell)
        {
            _portfolioCoordinator.SetCurrentPortfolio(item.Id, NormalizePortfolioName(item.Name), _shellState.CurrentPortfolioValue);
            _runtimeDataInvalidation.Invalidate("portfolio-selected");
        }
    }

    private void MarkSelectedPortfolio(Guid portfolioId)
    {
        foreach (ProfilePortfolioItem portfolio in Portfolios)
        {
            portfolio.IsSelected = portfolio.Id == portfolioId;
        }
    }

    private void LoadAvatarFromDisk()
    {
        AvatarBitmap = null;

        if (_userContext.UserId == Guid.Empty)
        {
            return;
        }

        string path = GetAvatarPath(_userContext.UserId);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            using MemoryStream stream = new(bytes);
            AvatarBitmap = new Bitmap(stream);
        }
        catch
        {
            AvatarBitmap = null;
        }
    }

    private void LoadProfileExtrasFromDisk()
    {
        if (_userContext.UserId == Guid.Empty)
        {
            return;
        }

        string path = GetLocationPath(_userContext.UserId);
        if (File.Exists(path))
        {
            string location = File.ReadAllText(path).Trim();
            if (!string.IsNullOrWhiteSpace(location))
            {
                Location = location;
            }
        }

        string legalProfilePath = GetLegalProfilePath(_userContext.UserId);
        if (File.Exists(legalProfilePath))
        {
            string raw = File.ReadAllText(legalProfilePath).Trim();
            if (Enum.TryParse(raw, ignoreCase: true, out LegalProfileKind parsed) && Enum.IsDefined(typeof(LegalProfileKind), parsed))
            {
                SelectedLegalProfile = parsed;
            }
        }
    }

    private void SaveProfileExtrasToDisk()
    {
        if (_userContext.UserId == Guid.Empty)
        {
            return;
        }

        Directory.CreateDirectory(GetProfileExtrasDirectory());
        File.WriteAllText(GetLocationPath(_userContext.UserId), string.IsNullOrWhiteSpace(Location) ? "Минск, Беларусь" : Location.Trim());
        File.WriteAllText(GetLegalProfilePath(_userContext.UserId), SelectedLegalProfile.ToString());
    }

    private static void DeleteProfileExtras(Guid userId)
    {
        string avatarPath = GetAvatarPath(userId);
        if (File.Exists(avatarPath))
        {
            File.Delete(avatarPath);
        }

        string locationPath = GetLocationPath(userId);
        if (File.Exists(locationPath))
        {
            File.Delete(locationPath);
        }

        string legalProfilePath = GetLegalProfilePath(userId);
        if (File.Exists(legalProfilePath))
        {
            File.Delete(legalProfilePath);
        }
    }

    private void RefreshRoleVisibility()
    {
        OnPropertyChanged(nameof(RoleDisplayName));
        OnPropertyChanged(nameof(IsPrivateInvestor));
        OnPropertyChanged(nameof(IsFinancialConsultant));
        OnPropertyChanged(nameof(IsPrivateInvestorSelected));
        OnPropertyChanged(nameof(IsFinancialConsultantSelected));
        OnPropertyChanged(nameof(ShowPortfolioManagement));
        OnPropertyChanged(nameof(HidePortfolioManagement));
        OnPropertyChanged(nameof(DeleteAccountButtonText));

        _addPortfolioCommand.RaiseCanExecuteChanged();
        _archivePortfolioCommand.RaiseCanExecuteChanged();
        _savePortfolioCommand.RaiseCanExecuteChanged();

        if (IsFinancialConsultant && !IsBusy && Portfolios.Count == 0)
        {
            _ = ReloadPortfoliosAsync();
        }
    }

    private void RefreshComputedProfileProperties()
    {
        OnPropertyChanged(nameof(RoleDisplayName));
        OnPropertyChanged(nameof(Initial));
        RefreshRoleVisibility();
    }

    private void RefreshBusyState()
    {
        OnPropertyChanged(nameof(IsBusy));
        _saveCommand.RaiseCanExecuteChanged();
        _reloadCommand.RaiseCanExecuteChanged();
        _addPortfolioCommand.RaiseCanExecuteChanged();
        _archivePortfolioCommand.RaiseCanExecuteChanged();
        _savePortfolioCommand.RaiseCanExecuteChanged();
        _saveTwelveDataKeyCommand.RaiseCanExecuteChanged();
        _deleteAccountCommand.RaiseCanExecuteChanged();
    }

    private static string NormalizePortfolioName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeDisplayName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Пользователь" : value.Trim();
    }

    private static string GetProfileExtrasDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Proxima",
            "Profile");
    }

    private static string GetAvatarPath(Guid userId)
    {
        return Path.Combine(GetProfileExtrasDirectory(), $"{userId:N}.avatar");
    }

    private static string GetLocationPath(Guid userId)
    {
        return Path.Combine(GetProfileExtrasDirectory(), $"{userId:N}.location");
    }

    private static string GetLegalProfilePath(Guid userId)
    {
        return Path.Combine(GetProfileExtrasDirectory(), $"{userId:N}.legal-profile");
    }


    private void NotifyIfFinalStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.EndsWith("...", StringComparison.Ordinal))
        {
            return;
        }

        string normalized = value.ToLowerInvariant();
        bool isSuccess = normalized.Contains("сохран", StringComparison.Ordinal)
            || normalized.Contains("создан", StringComparison.Ordinal)
            || normalized.Contains("удален", StringComparison.Ordinal)
            || normalized.Contains("обнов", StringComparison.Ordinal)
            || normalized.Contains("аватар", StringComparison.Ordinal);

        if (isSuccess)
        {
            _ = _notificationCenter.NotifyAsync(AppNotificationLevel.Success, "Профиль обновлен", value, "Профиль");
        }
    }

    private static string BuildErrorMessage(Exception ex)
    {
        Exception root = ex;
        while (root.InnerException is not null)
        {
            root = root.InnerException;
        }

        return ReferenceEquals(root, ex)
            ? ex.Message
            : $"{ex.Message} Внутренняя ошибка: {root.Message}";
    }



}

public enum LegalProfileKind
{
    PhysicalPerson,
    SelfEmployed,
    SoleProprietor,
    Company,
}

public sealed class ProfilePortfolioItem : ViewModelBase
{
    private bool _isSelected;
    private bool _isDirty;
    private string _name;
    private string _description;
    public ProfilePortfolioItem(Guid id, string name, string description)
    {
        Id = id;
        _name = name;
        _description = description;
    }

    public Guid Id { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                IsDirty = true;
            }
        }
    }

    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public void AcceptChanges(string name, string description)
    {
        _name = name;
        _description = description;
        _isDirty = false;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(IsDirty));
    }
}
