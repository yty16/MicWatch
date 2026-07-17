using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MicWatch.Helpers;
using MicWatch.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MicWatch.Services;

public partial class MicActivityMonitorService : ObservableObject, IMicActivityMonitorService
{
    public bool IsMicInUse { get; private set; }
    public bool IsMonitoring => _nativeMonitor is not null || _nonComMonitor is not null;
    public string? CurrentMicId { get; private set; }
    public string? LastError { get; private set; }

    [ObservableProperty]
    private string? _processName;

    public event Action<bool>? UsageChanged;

    private NativeMicActivityMonitor? _nativeMonitor;
    private NonComMicMonitor? _nonComMonitor;
    private DispatcherTimer? _pollTimer;
    private readonly IRulesetService _rulesetService;

    public MicActivityMonitorService(IRulesetService rulesetService)
    {
        _rulesetService = rulesetService;
        _rulesetService.RegisterRuleHandler("micwatch.ismicinuse", MicRuleHandler);
    }

    private bool MicRuleHandler(object? o)
    {
        if (o is MicRuleSettings settings)
        {
            return IsMicInUse && (!settings.MatchProcessName || string.Equals(ProcessName, settings.ProcessName, StringComparison.OrdinalIgnoreCase));
        }
        return false;
    }

    public async Task<IReadOnlyList<MicDeviceInfo>> GetAllMicsAsync()
    {
        var mics = await MicDeviceHelper.GetAllMicsAsync();
        return mics;
    }

    public async Task<(IReadOnlyList<MicDeviceInfo> Devices, string Diagnostic)> GetAllMicsWithDiagAsync()
    {
        return await Task.Run(() =>
        {
            var mics = MicDeviceHelper.GetAllMicsWithDiag(out string diag);
            return (mics.AsReadOnly(), diag);
        });
    }

    public void StartMonitoring(string micId)
    {
        LastError = null;

        if (CurrentMicId == micId && (_nativeMonitor is not null || _nonComMonitor is not null))
            return;

        StopMonitoring();

        bool isWaveIn = WaveInMicMonitor.TryParseDeviceId(micId, out uint waveDevId);

        if (!isWaveIn)
        {
            bool nativeOk = TryStartNativeMonitoring(micId);
            if (nativeOk) { CurrentMicId = micId; return; }

            LastError = "WASAPI 不可用，自动切换到非 COM 检测模式（进程 + 音频采样）";
            Debug.WriteLine($"[MicWatch] WASAPI failed, falling back to NonCom for device: {micId}");
        }

        StartNonComMonitoring(isWaveIn ? waveDevId : 0u);
        CurrentMicId = micId;
    }

    private bool TryStartNativeMonitoring(string micId)
    {
        var monitor = new NativeMicActivityMonitor(micId);
        monitor.UsageChanged += OnNativeUsageChanged;

        try
        {
            monitor.Start();
        }
        catch (Exception ex)
        {
            LastError = $"WASAPI 启动失败: {ex.Message}";
            monitor.UsageChanged -= OnNativeUsageChanged;
            monitor.Dispose();
            return false;
        }

        _nativeMonitor = monitor;
        IsMicInUse = false;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _pollTimer.Tick += (_, _) => _nativeMonitor?.PollOnce();
        _pollTimer.Start();
        return true;
    }

    private void StartNonComMonitoring(uint waveDeviceId)
    {
        var monitor = new NonComMicMonitor(waveDeviceId);
        monitor.UsageChanged += OnNonComUsageChanged;
        try
        {
            monitor.Start();
        }
        catch (Exception ex)
        {
            LastError = $"非 COM 监控启动失败: {ex.Message}";
            monitor.UsageChanged -= OnNonComUsageChanged;
            monitor.Dispose();
            return;
        }

        _nonComMonitor = monitor;
        IsMicInUse = false;
    }

    public void StopMonitoring()
    {
        if (_pollTimer is not null)
        {
            _pollTimer.Stop();
            _pollTimer = null;
        }

        if (_nativeMonitor is not null)
        {
            _nativeMonitor.UsageChanged -= OnNativeUsageChanged;
            _nativeMonitor.Dispose();
            _nativeMonitor = null;
        }

        if (_nonComMonitor is not null)
        {
            _nonComMonitor.UsageChanged -= OnNonComUsageChanged;
            _nonComMonitor.Dispose();
            _nonComMonitor = null;
        }

        CurrentMicId = null;
        IsMicInUse = false;
        ProcessName = null;
    }

    private void OnNativeUsageChanged(bool inUse, uint? pid)
    {
        SetUsage(inUse, pid.HasValue ? GetProcessName(pid.Value) : null);
    }

    private void OnNonComUsageChanged(bool inUse, string? app, string? method)
    {
        SetUsage(inUse, app ?? method ?? "非 COM 检测");
    }

    private static string? GetProcessName(uint pid)
    {
        try { return Process.GetProcessById((int)pid).ProcessName; }
        catch { return null; }
    }

    private void SetUsage(bool inUse, string? displayInfo)
    {
        IsMicInUse = inUse;
        ProcessName = displayInfo;
        UsageChanged?.Invoke(inUse);
        Dispatcher.UIThread.Post(() => _rulesetService.NotifyStatusChanged());
    }

    public void Dispose() => StopMonitoring();
}
