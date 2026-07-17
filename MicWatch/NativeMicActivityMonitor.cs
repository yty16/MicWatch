using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using MicWatch.Native;

namespace MicWatch;

public sealed class NativeMicActivityMonitor : IDisposable
{
    private readonly string _deviceId;
    private IMMDeviceEnumerator? _enumerator;
    private IAudioSessionManager2? _sessionManager;
    private bool _started;
    private bool _disposed;

    private Thread? _staThread;
    private readonly ConcurrentQueue<Action> _work = new();
    private readonly ManualResetEvent _signal = new(false);
    private readonly ManualResetEvent _initDone = new(false);
    private Exception? _initError;

    [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    public bool IsInUse { get; private set; }

    public event Action<bool, uint?>? UsageChanged;

    public NativeMicActivityMonitor(string deviceId) => _deviceId = deviceId;

    public void Start()
    {
        if (_started)
            return;

        _initError = null;
        _staThread = new Thread(StaProc) { IsBackground = true };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();
        _initDone.WaitOne();

        if (_initError is not null)
            throw _initError;

        _started = true;
    }

    private void StaProc()
    {
        try
        {
            IntPtr hMm = LoadLibrary("mmdevapi.dll");
            Debug.WriteLine($"[MicWatch] Monitor LoadLibrary(mmdevapi.dll) => 0x{hMm.ToInt64():X16}");

            CoreAudioNative.CoInitializeEx(IntPtr.Zero, COINIT.COINIT_APARTMENTTHREADED);

            try
            {
                int hr = CoreAudioNative.CreateMMDeviceEnumerator(out _enumerator!);
                if (hr < 0 || _enumerator is null)
                    throw new COMException($"CoCreateInstance MMDeviceEnumerator 失败: 0x{hr:X8}", hr);

                var device = GetTargetDevice(_enumerator);
                device.Activate(CoreAudioGuids.IID_IAudioSessionManager2, CLSCTX.ALL, IntPtr.Zero, out var mgrObj);
                _sessionManager = (IAudioSessionManager2)mgrObj;
                Marshal.ReleaseComObject(device);

                _initDone.Set();

                while (!_disposed)
                {
                    _signal.WaitOne();
                    while (_work.TryDequeue(out var action))
                    {
                        try { action(); }
                        catch { }
                    }

                    _signal.Reset();
                }
            }
            finally
            {
                if (_sessionManager is not null)
                {
                    Marshal.ReleaseComObject(_sessionManager);
                    _sessionManager = null;
                }

                if (_enumerator is not null)
                {
                    Marshal.ReleaseComObject(_enumerator);
                    _enumerator = null;
                }

                CoreAudioNative.CoUninitialize();
            }
        }
        catch (Exception ex)
        {
            _initError = ex;
            _initDone.Set();
        }
    }

    private IMMDevice GetTargetDevice(IMMDeviceEnumerator enumerator)
    {
        if (!string.IsNullOrEmpty(_deviceId) && !_deviceId.StartsWith("wavein:"))
        {
            try
            {
                enumerator.GetDevice(_deviceId, out var dev);
                return dev;
            }
            catch (COMException)
            {
            }
        }

        enumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eCommunications, out var def);
        return def;
    }

    public void PollOnce()
    {
        if (!_started)
            return;

        _work.Enqueue(PollOnceOnSta);
        _signal.Set();
    }

    private void PollOnceOnSta()
    {
        if (_sessionManager is null)
            return;

        _sessionManager.GetSessionEnumerator(out var enumerator);
        enumerator.GetCount(out var count);

        uint? pid = null;
        for (int i = 0; i < count; i++)
        {
            enumerator.GetSession(i, out var control);
            if (control is null)
                continue;

            try
            {
                var ctrl2 = (IAudioSessionControl2)control;
                ctrl2.GetState(out var state);
                if (state != AudioSessionState.AudioSessionStateActive)
                    continue;

                bool isSystemSounds;
                try
                {
                    isSystemSounds = ctrl2.IsSystemSoundsSession() == 0;
                }
                catch (COMException)
                {
                    isSystemSounds = true;
                }

                if (isSystemSounds)
                    continue;

                ctrl2.GetProcessId(out var processId);
                pid = processId;
                break;
            }
            finally
            {
                Marshal.ReleaseComObject(control);
            }
        }

        Marshal.ReleaseComObject(enumerator);

        bool inUse = pid.HasValue;
        if (inUse != IsInUse)
        {
            IsInUse = inUse;
            UsageChanged?.Invoke(inUse, pid);
        }
    }

    public void Stop()
    {
        if (!_started)
            return;

        _disposed = true;
        _signal.Set();
        _staThread?.Join(2000);
        _started = false;
    }

    public void Dispose() => Stop();
}
