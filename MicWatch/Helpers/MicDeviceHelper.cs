using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using MicWatch.Models;
using MicWatch.Native;

namespace MicWatch.Helpers;

public static class MicDeviceHelper
{
    private const ushort VT_LPWSTR = 31;

    public static string? LastDiagnostic { get; private set; }

    [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    #region Win32 waveIn API（Unicode，纯 P/Invoke，无需 COM）

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WAVEINCAPS
    {
        public short wMid;
        public short wPid;
        public int vDriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public uint dwFormats;
        public short wChannels;
        [MarshalAs(UnmanagedType.U2)] public ushort wReserved1;
        [MarshalAs(UnmanagedType.U2)] public ushort wReserved2;
    }

    [DllImport("winmm")]
    private static extern uint waveInGetNumDevs();

    [DllImport("winmm", EntryPoint = "waveInGetDevCapsW")]
    private static extern int waveInGetDevCaps(uint uDeviceID, out WAVEINCAPS lpCaps, uint cbwCaps);

    internal const int MMSYSERR_NOERROR = 0;
    internal const int MMSYSERR_ALLOCATED = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [DllImport("winmm")]
    private static extern int waveInOpen(out IntPtr phwi, uint uDeviceID, ref WAVEFORMATEX pwfx,
        IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);

    internal const uint WAVE_FORMAT_QUERY = 0x0001;
    internal const int WAVE_FORMAT_PCM = 1;

    internal static bool IsWaveInDeviceInUse(uint deviceId)
    {
        var fmt = new WAVEFORMATEX
        {
            wFormatTag = WAVE_FORMAT_PCM,
            nChannels = 1,
            nSamplesPerSec = 44100,
            nAvgBytesPerSec = 88200,
            nBlockAlign = 2,
            wBitsPerSample = 16,
            cbSize = 0
        };
        return waveInOpen(out _, deviceId, ref fmt, IntPtr.Zero, IntPtr.Zero, WAVE_FORMAT_QUERY) == MMSYSERR_ALLOCATED;
    }

    private static List<MicDeviceInfo> EnumerateViaWaveIn()
    {
        var result = new List<MicDeviceInfo>();
        uint count = waveInGetNumDevs();
        for (uint i = 0; i < count; i++)
        {
            try
            {
                waveInGetDevCaps(i, out var caps, (uint)Marshal.SizeOf<WAVEINCAPS>());
                string name = caps.szPname.TrimEnd('\0');
                if (string.IsNullOrWhiteSpace(name))
                    name = $"录音设备{i}";
                result.Add(new MicDeviceInfo
                {
                    Id = $"wavein:{i}",
                    Name = name
                });
            }
            catch { }
        }
        return result;
    }

    #endregion

    private static List<MicDeviceInfo> EnumerateOnStaThread(out string diagnostic)
    {
        var diag = new StringBuilder();
        List<MicDeviceInfo> result = [];
        Exception? lastError = null;

        var staThread = new Thread(() =>
        {
            try
            {
                IntPtr hMmdevapi = LoadLibrary("mmdevapi.dll");
                diag.AppendLine($"LoadLibrary(mmdevapi.dll) => 0x{hMmdevapi.ToInt64():X16} {(hMmdevapi == IntPtr.Zero ? "(FAIL)" : "OK")}");

                int hrInit = CoreAudioNative.CoInitializeEx(IntPtr.Zero, COINIT.COINIT_APARTMENTTHREADED);
                diag.AppendLine($"CoInitializeEx(STA) => 0x{hrInit:X8} ({HrStr(hrInit)})");

                IMMDeviceEnumerator? enumerator = null;
                bool comFailed = false;

                int hrCreate = CoreAudioNative.CreateMMDeviceEnumerator(out enumerator!);
                if (hrCreate < 0 || enumerator is null)
                {
                    comFailed = true;
                    diag.AppendLine($"创建 MMDeviceEnumerator => 0x{hrCreate:X8} ({HrStr(hrCreate)})");
                    diag.AppendLine(">>> COM 枚举失败，切换到 Win32 waveIn 兜底 <<<");
                }
                else
                {
                    diag.AppendLine("创建 MMDeviceEnumerator => OK (WASAPI 可用)");
                }

                if (!comFailed && enumerator is not null)
                {
                    try
                    {
                        enumerator.EnumAudioEndpoints(
                            EDataFlow.eCapture,
                            DeviceState.DEVICE_STATE_ACTIVE | DeviceState.DEVICE_STATE_UNPLUGGED | DeviceState.DEVICE_STATE_DISABLED,
                            out var collection);
                        collection.GetCount(out var count);
                        diag.AppendLine($"EnumAudioEndpoints(eCapture) => OK, count={count}");

                        for (uint i = 0; i < count; i++)
                        {
                            collection.Item(i, out var device);
                            device.GetId(out IntPtr idPtr);
                            string id = Marshal.PtrToStringUni(idPtr) ?? "";
                            Marshal.FreeCoTaskMem(idPtr);

                            string name = id;
                            try
                            {
                                device.OpenPropertyStore(STGM.READ, out var props);
                                props.GetValue(CoreAudioGuids.PKEY_Device_FriendlyName, out var pv);
                                if (pv.vt == VT_LPWSTR && pv.pwszVal != IntPtr.Zero)
                                    name = Marshal.PtrToStringUni(pv.pwszVal) ?? id;
                                CoreAudioNative.PropVariantClear(ref pv);
                                Marshal.ReleaseComObject(props);
                            }
                            catch { }

                            diag.AppendLine($"  [{i}] {name}");
                            result.Add(new MicDeviceInfo { Id = id, Name = name });
                            Marshal.ReleaseComObject(device);
                        }

                        Marshal.ReleaseComObject(collection);
                    }
                    catch (Exception ex)
                    {
                        diag.AppendLine($"WASAPI 枚举异常: {ex.GetType().Name}: {ex.Message}");
                        comFailed = true;
                    }

                    Marshal.ReleaseComObject(enumerator);
                }

                if (comFailed || result.Count == 0)
                {
                    diag.AppendLine("\n--- Win32 waveIn 兜底枚举 ---");
                    var waveDevices = EnumerateViaWaveIn();
                    foreach (var d in waveDevices)
                    {
                        diag.AppendLine($"  [wavein] {d.Name}");
                        result.Add(d);
                    }
                    diag.AppendLine($"waveIn 共找到 {waveDevices.Count} 个音频输入设备");
                }

                if (result.Count == 0 && !comFailed)
                {
                    diag.AppendLine("--- WASAPI 0 设备 + COM 正常：尝试默认端点 ---");
                    try
                    {
                        hrCreate = CoreAudioNative.CreateMMDeviceEnumerator(out enumerator!);
                        if (hrCreate >= 0 && enumerator is not null)
                        {
                            enumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eConsole, out var defDevice);
                            defDevice.GetId(out IntPtr idPtr);
                            string id = Marshal.PtrToStringUni(idPtr) ?? "";
                            Marshal.FreeCoTaskMem(idPtr);
                            string name = id;
                            try
                            {
                                defDevice.OpenPropertyStore(STGM.READ, out var props);
                                props.GetValue(CoreAudioGuids.PKEY_Device_FriendlyName, out var pv);
                                if (pv.vt == VT_LPWSTR && pv.pwszVal != IntPtr.Zero)
                                    name = Marshal.PtrToStringUni(pv.pwszVal) ?? id;
                                CoreAudioNative.PropVariantClear(ref pv);
                                Marshal.ReleaseComObject(props);
                            }
                            catch { }
                            result.Add(new MicDeviceInfo { Id = id, Name = name + "（默认捕获设备）" });
                            Marshal.ReleaseComObject(defDevice);
                            diag.AppendLine($"GetDefaultAudioEndpoint => {name}");
                            Marshal.ReleaseComObject(enumerator);
                        }
                    }
                    catch (Exception ex)
                    {
                        diag.AppendLine($"GetDefaultAudioEndpoint => {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
                diag.AppendLine($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException is not null)
                    diag.AppendLine($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");

                diag.AppendLine("\n--- 最后兜底：Win32 waveIn ---");
                try
                {
                    var waveDevices = EnumerateViaWaveIn();
                    foreach (var d in waveDevices)
                    {
                        diag.AppendLine($"  [wavein] {d.Name}");
                        result.Add(d);
                    }
                    lastError = null;
                }
                catch (Exception ex2)
                {
                    diag.AppendLine($"waveIn 也失败: {ex2.Message}");
                }
            }
            finally
            {
                CoreAudioNative.CoUninitialize();
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        diagnostic = diag.ToString();
        LastDiagnostic = diagnostic;

        Debug.WriteLine($"[MicWatch] {diagnostic}");
        return result;
    }

    public static List<MicDeviceInfo> GetAllMics() => EnumerateOnStaThread(out _);

    public static List<MicDeviceInfo> GetAllMicsWithDiag(out string diag) => EnumerateOnStaThread(out diag);

    public static Task<List<MicDeviceInfo>> GetAllMicsAsync() => Task.Run(GetAllMics);

    private static string HrStr(int hr) => hr switch
    {
        0 => "S_OK",
        1 => "S_FALSE",
        unchecked((int)0x80040154) => "REGDB_E_CLASSNOTREG",
        unchecked((int)0x80070005) => "E_ACCESSDENIED",
        unchecked((int)0x80004002) => "E_NOINTERFACE",
        unchecked((int)0x88880004) => "AUDCLNT_E_DEVICE_INVALIDATED",
        _ => $"0x{hr:X8}"
    };
}
