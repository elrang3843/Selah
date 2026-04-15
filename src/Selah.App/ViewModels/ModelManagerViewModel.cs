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
    private double _downloadProgress;   // 0~100, -1 = 숨김
    private bool _showDownloadProgress;

    public ModelManagerViewModel(ModelManagerService service)
    {
        _statusMessage = Loc.Get("Status_ModelManager");
        _service = service;
        Models = new ObservableCollection<ModelInfo>(service.GetCatalog());

        InstallDemucsCommand = new AsyncRelayCommand(InstallSelectedAsync, () => !IsBusy);
        RefreshCommand       = new RelayCommand(Refresh, () => !IsBusy);
        OpenSourceUrlCommand  = new RelayCommand<string>(OpenUrl);
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

    public string InstallLog
    {
        get => _installLog;
        private set => SetField(ref _installLog, value);
    }

    public bool ShowInstallLog
    {
        get => _showInstallLog;
        private set => SetField(ref _showInstallLog, value);
    }

    /// <summary>다운로드 진행률 (0~100). ShowDownloadProgress が true のとき表示。</summary>
    public double DownloadProgress
    {
        get => _downloadProgress;
        private set => SetField(ref _downloadProgress, value);
    }

    public bool ShowDownloadProgress
    {
        get => _showDownloadProgress;
        private set => SetField(ref _showDownloadProgress, value);
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
        ShowInstallLog      = false;
        ShowDownloadProgress = false;
        InstallLog          = string.Empty;
    }

    private async Task InstallSelectedAsync()
    {
        // 모델이 선택되지 않았으면 첫 번째 ONNX 모델 사용
        var target = SelectedModel
            ?? Models.FirstOrDefault(m => m.Engine == ModelEngine.OnnxRuntime);

        if (target == null)
        {
            StatusMessage = "설치할 모델이 없습니다.";
            return;
        }

        IsBusy              = true;
        InstallLog          = string.Empty;
        ShowInstallLog      = true;
        ShowDownloadProgress = false;
        DownloadProgress    = 0;

        void AppendLog(string line)
        {
            // BYTES:<received>/<total> 형식 → 진행률 바 업데이트
            if (line.StartsWith("BYTES:", StringComparison.Ordinal))
            {
                var parts = line[6..].Split('/');
                if (parts.Length == 2 &&
                    long.TryParse(parts[0].Split(' ')[0], out long recv) &&
                    long.TryParse(parts[1].Split(' ')[0], out long total) &&
                    total > 0)
                {
                    ShowDownloadProgress = true;
                    DownloadProgress     = recv * 100.0 / total;
                    StatusMessage        = line;
                    return;
                }
            }
            InstallLog    += line + "\n";
            StatusMessage  = line;
        }

        AppendLog($"▶ {target.Name} 설치 시작");
        AppendLog(string.Empty);

        try
        {
            await _service.SetupModelAsync(target, new Progress<string>(AppendLog));

            AppendLog(string.Empty);
            AppendLog("✓ " + Loc.Get("Status_ModelSetupComplete"));
            StatusMessage = Loc.Get("Status_ModelSetupComplete");

            _service.RefreshInstallStatus();
            var updated = _service.GetCatalog();
            for (int i = 0; i < Models.Count && i < updated.Count; i++)
            {
                Models[i].IsInstalled = updated[i].IsInstalled;
                Models[i].LocalPath   = updated[i].LocalPath;
            }
            OnPropertyChanged(nameof(AnyModelInstalled));
        }
        catch (OperationCanceledException)
        {
            AppendLog("✗ 설치가 취소되었습니다.");
            StatusMessage = "설치 취소됨";
        }
        catch (Exception ex)
        {
            AppendLog(string.Empty);
            AppendLog("✗ " + Loc.Format("Status_InstallFailed", ex.Message));
            StatusMessage = Loc.Format("Status_InstallFailed", ex.Message);
        }
        finally
        {
            IsBusy               = false;
            ShowDownloadProgress = false;
        }
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = url,
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
        _execute    = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add    => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? p) => _canExecute?.Invoke((T?)p) ?? true;
    public void Execute(object? p) => _execute((T?)p);
}
