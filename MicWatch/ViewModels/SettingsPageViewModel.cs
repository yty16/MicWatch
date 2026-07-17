using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicWatch.Helpers;
using MicWatch.Models;
using MicWatch.Services;
using System.Collections.ObjectModel;

namespace MicWatch.ViewModels;

public partial class SettingsPageViewModel : ObservableObject
{
    public IMicActivityMonitorService MicActivityMonitorService { get; }
    public bool IsMonitoring => MicActivityMonitorService.IsMonitoring;
    public ObservableCollection<MicDeviceInfo> Mics { get; } = [];

    public bool IsMicInUse => MicActivityMonitorService.IsMicInUse;

    public static Settings Settings => Plugin.Settings;

    public bool CanStartMonitor => !IsMonitoring;

    public string? EnumError { get; private set; }

    public MicDeviceInfo? SelectedMic
    {
        get => Plugin.Settings.SelectedMic;
        set
        {
            if (SetProperty(Plugin.Settings.SelectedMic,
                            value,
                            Plugin.Settings,
                            (s, v) => s.SelectedMic = v))
            {
                OnSelectedMicChanged(value);
            }
        }
    }

    public SettingsPageViewModel(IMicActivityMonitorService micActivityMonitorService)
    {
        MicActivityMonitorService = micActivityMonitorService;
        MicActivityMonitorService.UsageChanged += (_) => OnPropertyChanged(nameof(IsMicInUse));

        if (Settings.AutoStart)
        {
            StartMonitor();
        }
    }

    private void OnSelectedMicChanged(MicDeviceInfo? value)
    {
        if (IsMonitoring == false)
        {
            return;
        }
        StartMonitor();
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        EnumError = null;
        OnPropertyChanged(nameof(EnumError));
        try
        {
            var (devices, diag) = await MicActivityMonitorService.GetAllMicsWithDiagAsync();
            var previousId = SelectedMic?.Id;
            Mics.Clear();

            foreach (var device in devices)
            {
                Mics.Add(device);
            }

            if (Mics.Count == 0)
            {
                SelectedMic = null;
                MicActivityMonitorService.StopMonitoring();

                var permStatus = MicPermissionHelper.GetStatusText();
                EnumError = $"未枚举到任何麦克风设备。\n\n{permStatus}\n\n诊断信息：\n{diag}";
                OnPropertyChanged(nameof(EnumError));
                return;
            }

            bool hasWaveInOnly = Mics.All(m => m.Id.StartsWith("wavein:"));
            if (hasWaveInOnly && Mics.Count > 0)
            {
                EnumError = "⚠️ WASAPI COM 不可用，已使用 Win32 兜底模式。\n监控将使用「进程检测 + 音频采样」组合方案（非 WASAPI 会话模式）。";
                OnPropertyChanged(nameof(EnumError));
            }

            SelectedMic = Mics.FirstOrDefault(x => x.Id == previousId) ?? Mics[0];
        }
        catch (Exception ex)
        {
            MicActivityMonitorService.StopMonitoring();
            EnumError = $"枚举麦克风失败：{ex.GetType().Name} - {ex.Message}";
            OnPropertyChanged(nameof(EnumError));
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartMonitor))]
    private void StartMonitor()
    {
        if (SelectedMic is null)
        {
            return;
        }
        MicActivityMonitorService.StartMonitoring(SelectedMic.Id);

        if (!string.IsNullOrEmpty(MicActivityMonitorService.LastError))
        {
            EnumError = $"启动监控失败：{MicActivityMonitorService.LastError}";
            OnPropertyChanged(nameof(EnumError));
            return;
        }

        OnPropertyChanged(nameof(IsMonitoring));
        OnPropertyChanged(nameof(IsMicInUse));
        OnPropertyChanged(nameof(CanStartMonitor));
        StartMonitorCommand.NotifyCanExecuteChanged();
        StopMonitorCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsMonitoring))]
    private void StopMonitor()
    {
        if (!IsMonitoring)
        {
            return;
        }
        MicActivityMonitorService.StopMonitoring();
        OnPropertyChanged(nameof(IsMonitoring));
        OnPropertyChanged(nameof(IsMicInUse));
        OnPropertyChanged(nameof(CanStartMonitor));
        StartMonitorCommand.NotifyCanExecuteChanged();
        StopMonitorCommand.NotifyCanExecuteChanged();
    }
}
