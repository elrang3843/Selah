using System.Collections.ObjectModel;
using System.Windows.Input;
using Selah.Core.Models;
using Selah.Core.Services;

namespace Selah.App.ViewModels;

public class ModelManagerViewModel : ViewModelBase
{
    private readonly ModelManagerService _service;
    private ModelInfo? _selectedModel;
    private string _statusMessage = "모델 관리자";
    private bool _isBusy;

    public ModelManagerViewModel(ModelManagerService service)
    {
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
        StatusMessage = "모델 목록 새로고침 완료";
    }

    private async Task InstallDemucsAsync()
    {
        IsBusy = true;
        StatusMessage = "Demucs 설치 준비 중...";
        try
        {
            await _service.InstallDemucsAsync(
                new Progress<string>(msg => StatusMessage = msg));
            Refresh();
            StatusMessage = "Demucs 설치 완료! MR 추출 기능을 사용할 수 있습니다.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"설치 실패: {ex.Message}";
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
