using MicWatch.Models;

namespace MicWatch.Services;

public interface IMicActivityMonitorService : IDisposable
{
    public bool IsMicInUse { get; }
    public bool IsMonitoring { get; }
    public string? CurrentMicId { get; }
    public string? LastError { get; }

    public string? ProcessName { get; }

    Task<IReadOnlyList<MicDeviceInfo>> GetAllMicsAsync();
    Task<(IReadOnlyList<MicDeviceInfo> Devices, string Diagnostic)> GetAllMicsWithDiagAsync();

    public event Action<bool>? UsageChanged;

    public void StartMonitoring(string micId);
    public void StopMonitoring();
}
