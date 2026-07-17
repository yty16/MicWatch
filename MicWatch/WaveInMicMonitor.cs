using System.Diagnostics;
using MicWatch.Helpers;

namespace MicWatch;

public sealed class WaveInMicMonitor : IDisposable
{
    private readonly uint _deviceId;
    private Timer? _pollTimer;

    public bool IsInUse { get; private set; }

    public event Action<bool>? UsageChanged;

    public WaveInMicMonitor(string deviceId)
    {
        if (!TryParseDeviceId(deviceId, out uint devId))
            throw new ArgumentException($"无效的 waveIn 设备 ID: {deviceId}", nameof(deviceId));
        _deviceId = devId;
    }

    public void Start()
    {
        if (_pollTimer is not null)
            return;

        IsInUse = false;
        _pollTimer = new Timer(_ => PollOnce(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }

    public void Stop()
    {
        var t = Interlocked.Exchange(ref _pollTimer, null);
        if (t is not null)
        {
            t.Dispose();
        }
        IsInUse = false;
    }

    private void PollOnce()
    {
        try
        {
            bool inUse = MicDeviceHelper.IsWaveInDeviceInUse(_deviceId);
            if (inUse != IsInUse)
            {
                IsInUse = inUse;
                UsageChanged?.Invoke(inUse);
                Debug.WriteLine($"[MicWatch] waveIn:{_deviceId} => {(inUse ? "IN USE" : "FREE")}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicWatch] waveIn poll error (device {_deviceId}): {ex.Message}");
        }
    }

    public void Dispose() => Stop();

    internal static bool TryParseDeviceId(string deviceId, out uint devId)
    {
        devId = 0;
        if (!deviceId.StartsWith("wavein:", StringComparison.OrdinalIgnoreCase))
            return false;
        return uint.TryParse(deviceId.AsSpan("wavein:".Length), out devId);
    }
}
