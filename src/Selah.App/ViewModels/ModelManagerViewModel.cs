using System.Collections.ObjectModel;
using System.Windows.Input;
using Selah.Core.Models;
using Selah.Core.Services;

namespace Selah.App.ViewModels;

public class ModelManagerViewModel : ViewModelBase
{
    private readonly ModelManagerService _service;
    private ModelInfo? _selectedModel;
    private string _statusMessage;
    private bool _isBusy;
    private string _installLog = string.Empty;
    private bool _showInstallLog;

    public ModelManagerViewModel(ModelManagerService service)
    {
        _statusMessage = Loc.Get("Status_ModelManager");
        _service = service;
        Models = new ObservableCollection<ModelInfo>(service.GetCatalog());

        InstallDemucsCommand = new AsyncRelayCommand(InstallDemucsAsync, () => !IsBusy);
        RefreshCommand = new RelayCommand(Refresh, () => !IsBusy);
        OpenSourceUrlCommand = new RelayCommand<string>(OpenUrl);
        OpenLicenseUrlCommand = new RelayCommand<string>(OpenUrl);
    }

    public ObservableCollection<ModelInfo> Models { get; }

    public ModelInfo? SelectedModel
    {
        get => _selectedModel;
        set => SetField(ref _selectedModel, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public bool AnyModelInstalled => Models.Any(m => m.IsInstalled);

    /// <summary>pip 설치 출력 로그 (설치 중/후 오른쪽 패널에 표시)</summary>
    public string InstallLog
    {
        get => _installLog;
        private set => SetField(ref _installLog, value);
    }

    /// <summary>설치 로그 패널 표시 여부 (Refresh 시 false로 초기화)</summary>
    public bool ShowInstallLog
    {
        get => _showInstallLog;
        private set => SetField(ref _showInstallLog, value);
    }

    public ICommand InstallDemucsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenSourceUrlCommand { get; }
    public ICommand OpenLicenseUrlCommand { get; }

    private void Refresh()
    {
        _service.RefreshInstallStatus();
        Models.Clear();
        foreach (var m in _service.GetCatalog())
            Models.Add(m);
        OnPropertyChanged(nameof(AnyModelInstalled));
        StatusMessage = Loc.Get("Status_ModelRefreshed");
        // 새로고침 시 로그 패널 닫기
        ShowInstallLog = false;
        InstallLog = string.Empty;
    }

    private async Task InstallDemucsAsync()
    {
        IsBusy = true;
        InstallLog = string.Empty;
        ShowInstallLog = true;

        void AppendLog(string line)
        {
            InstallLog += line + "\n";
            StatusMessage = line;
        }

        AppendLog("▶ pip install demucs");
        AppendLog(string.Empty);

        try
        {
            await _service.InstallDemucsAsync(
                new Progress<string>(AppendLog));

            AppendLog(string.Empty);
            AppendLog("✓ " + Loc.Get("Status_DemucsInstalled"));
            StatusMessage = Loc.Get("Status_DemucsInstalled");

            // 설치 완료 후 모델 목록 갱신 (로그 패널은 유지)
            _service.RefreshInstallStatus();
            var updatedCatalog = _service.GetCatalog();
            for (int i = 0; i < Models.Count && i < updatedCatalog.Count; i++)
            {
                Models[i].IsInstalled = updatedCatalog[i].IsInstalled;
                Models[i].LocalPath  = updatedCatalog[i].LocalPath;
            }
            OnPropertyChanged(nameof(AnyModelInstalled));
        }
        catch (Exception ex)
        {
            AppendLog(string.Empty);
            AppendLog("✗ " + Loc.Format("Status_InstallFailed", ex.Message));
            StatusMessage = Loc.Format("Status_InstallFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { /* 무시 */ }
    }
}

/// <summary>제네릭 RelayCommand</summary>
public class RelayCommand<T> : System.Windows.Input.ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? p) => _canExecute?.Invoke((T?)p) ?? true;
    public void Execute(object? p) => _execute((T?)p);
}
