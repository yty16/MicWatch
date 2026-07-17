using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MicWatch.Helpers;

namespace MicWatch;

public sealed class NonComMicMonitor : IDisposable
{
    private readonly uint _waveInDeviceId;
    private Timer? _pollTimer;

    public bool IsInUse { get; private set; }
    public string? DetectedAppName { get; private set; }
    public string? DetectionMethod { get; private set; }

    public event Action<bool, string?, string?>? UsageChanged;

    public NonComMicMonitor(uint waveInDeviceId)
    {
        _waveInDeviceId = waveInDeviceId;
    }

    public void Start()
    {
        if (_pollTimer is not null)
            return;

        IsInUse = false;
        DetectedAppName = null;
        DetectionMethod = null;
        _pollTimer = new Timer(_ => PollOnce(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(800));
    }

    public void Stop()
    {
        var t = Interlocked.Exchange(ref _pollTimer, null);
        if (t is not null)
            t.Dispose();
        IsInUse = false;
        DetectedAppName = null;
        DetectionMethod = null;
    }

    private void PollOnce()
    {
        try
        {
            var result = DetectMicUsage();
            bool nowInUse = result.InUse;
            string? method = result.Method;
            string? app = result.AppName;

            if (nowInUse != IsInUse || app != DetectedAppName)
            {
                IsInUse = nowInUse;
                DetectedAppName = app;
                DetectionMethod = method;
                UsageChanged?.Invoke(nowInUse, app, method);
                Debug.WriteLine($"[MicWatch] NonCom => {(nowInUse ? "IN USE" : "FREE")} via {method} {(app is not null ? $"[{app}]" : "")}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicWatch] NonCom poll error: {ex.Message}");
        }
    }

    internal sealed class DetectionResult
    {
        public bool InUse { get; set; }
        public string? Method { get; set; }
        public string? AppName { get; set; }
    }

    #region 方法 1：进程启发式检测（最可靠）

    private static readonly string[] KnownAudioAppPatterns =
    {
        "WeChat",          "wechat",         "微信",
        "Teams",           "teams",          "Microsoft.Teams",
        "Zoom",            "zoom",           "Zoom Meeting",
        "Discord",         "discord",
        "Skype",           "skype",
        "QQ",              "qq",
        "DingTalk",        "dingtalk",       "钉钉",
        "Lark",            "lark",           "飞书",
        "obs",             "OBS",
        "audacity",
        "SoundRecorder",   "soundrecorder",  "录音",
        "VoiceRecorder",   "voicerecorder",
        "XSplit",
        "Streamlabs",
        "GameBar",         "xboxapp",        "Xbox Game Bar",
        "NVIDIA Share",    "nvcontainer",    "GeForceExperience",
        "frps",
        "OBSStudio",
        "Telegram",        "telegram",
        "Slack",
        "FaceTime",        "facetime",
        "Google Meet",     "meet",
    };

    private static readonly ConcurrentDictionary<string, string> _processNameCache = new();

    private static DetectionResult CheckAudioProcesses()
    {
        try
        {
            Process[] procs = Process.GetProcesses();
            foreach (var p in procs)
            {
                string procName;
                try { procName = p.ProcessName; }
                catch { continue; }

                if (string.IsNullOrEmpty(procName))
                    continue;

                string? match = FindAudioMatch(procName);
                if (match is not null)
                {
                    string title;
                    try { title = p.MainWindowTitle ?? ""; }
                    catch { title = ""; }

                    string display = !string.IsNullOrWhiteSpace(title) && title.Length < 50
                        ? $"{procName} ({title})"
                        : procName;

                    return new DetectionResult
                    {
                        InUse = true,
                        Method = "进程检测",
                        AppName = display,
                    };
                }
            }
        }
        catch { }

        return new DetectionResult { InUse = false, Method = null };
    }

    private static string? FindAudioMatch(string procName)
    {
        if (_processNameCache.TryGetValue(procName, out var cached))
            return cached;

        string lower = procName.ToLowerInvariant();
        foreach (var pattern in KnownAudioAppPatterns)
        {
            if (lower.Contains(pattern.ToLowerInvariant()))
            {
                _processNameCache[procName] = pattern;
                return pattern;
            }
        }

        _processNameCache[procName] = null!;
        return null;
    }

    #endregion

    #region 方法 2：Mixer 峰值电平表（辅助验证）

    [StructLayout(LayoutKind.Sequential)]
    private struct MIXERCONTROLDETAILS_UNSIGNED
    {
        public uint dwValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIXERCONTROL
    {
        public int cbStruct;
        public int dwControlType;
        public int dwControlID;
        public IntPtr dwControlTypeCustom;
        public int fdwControl;
        public int cMultipleItems;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szShortName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szName;
        public int lMinimum;
        public int lMaximum;
        [MarshalAs(UnmanagedType.U4)]
        public uint[] reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIXERLINECONTROLS
    {
        public int cbStruct;
        public int dwLineID;
        [MarshalAs(UnmanagedType.U4)]
        public int dwControlType;
        public int cControls;
        public int cbmxctrl;
        public IntPtr pamxctrl;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIXERLINE
    {
        public int cbStruct;
        public int dwDestination;
        public int dwSource;
        public int dwLineID;
        public int dwStatus;
        public int dwUser;
        public int dwComponentType;
        public int cChannels;
        public int cConnections;
        public int cControls;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string szShortName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szName;
        public IntPtr dwType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIXERCONTROLDETAILS
    {
        public int cbStruct;
        public int dwControlID;
        public int cChannels;
        public IntPtr hwndOwner;
        public int cMultipleItems;
        public IntPtr paDetails;
    }

    private const int MIXER_OBJECT_WAVEIN = 0x40010000;
    private const int MIXER_GETLINEINFOF_SOURCE = 2;
    private const int MIXER_GETLINECONTROLSF_ONEBYTYPE = 4;
    private const int MIXERCONTROL_CT_CLASS_METER = 0x20000000;
    private const int MIXERCONTROL_CT_SC_PEAKMETER = 0x01000000;
    private const int MIXER_SETCONTROLDETAILSF_VALUE = 0;
    private const int MIXER_GETCONTROLDETAILSF_VALUE = 0;
    private const int MIXERLINE_COMPONENTTYPE_DST_FIRST = 0;
    private const int MIXERLINE_COMPONENTTYPE_SRC_MICROPHONE = 0x00010001 + 7;
    private const int MMSYSERR_NOERROR = 0;

    [DllImport("winmm")]
    private static extern int mixerOpen(out IntPtr phmx, int uMxId, IntPtr pCallback, IntPtr dwInstance, int fdwOpen);

    [DllImport("winmm")]
    private static extern int mixerClose(IntPtr hmx);

    [DllImport("winmm")]
    private static extern int mixerGetLineInfo(IntPtr hmx, ref MIXERLINE pmxl, int fdwInfo);

    [DllImport("winmm")]
    private static extern int mixerGetLineControls(IntPtr hmx, ref MIXERLINECONTROLS pmxlc, int fdwControls);

    [DllImport("winmm")]
    private static extern int mixerGetControlDetails(IntPtr hmx, ref MIXERCONTROLDETAILS pmxcd, int fdwDetails);

    private static bool TryReadPeakMeter(uint deviceId, out int peakValue)
    {
        peakValue = 0;
        IntPtr hMixer;
        int ret = mixerOpen(out hMixer, (int)deviceId, IntPtr.Zero, IntPtr.Zero, MIXER_OBJECT_WAVEIN);
        if (ret != MMSYSERR_NOERROR || hMixer == IntPtr.Zero)
            return false;

        try
        {
            var line = new MIXERLINE
            {
                cbStruct = Marshal.SizeOf<MIXERLINE>(),
                dwSource = 0,
                dwComponentType = MIXERLINE_COMPONENTTYPE_SRC_MICROPHONE,
            };
            ret = mixerGetLineInfo(hMixer, ref line, MIXER_GETLINEINFOF_SOURCE | MIXERLINE_COMPONENTTYPE_DST_FIRST);
            if (ret != MMSYSERR_NOERROR || line.cControls == 0)
                return false;

            var ctrl = new MIXERCONTROL { cbStruct = Marshal.SizeOf<MIXERCONTROL>() };

            var ctrlHeader = new MIXERLINECONTROLS
            {
                cbStruct = Marshal.SizeOf<MIXERLINECONTROLS>(),
                dwLineID = line.dwLineID,
                dwControlType = MIXERCONTROL_CT_CLASS_FADER | MIXERCONTROL_CT_SC_PEAKMETER,
                cControls = 1,
                cbmxctrl = Marshal.SizeOf<MIXERCONTROL>(),
                pamxctrl = Marshal.AllocHGlobal(Marshal.SizeOf<MIXERCONTROL>()),
            };

            try
            {
                Marshal.StructureToPtr(ctrl, ctrlHeader.pamxctrl, false);
                ret = mixerGetLineControls(hMixer, ref ctrlHeader, MIXER_GETLINECONTROLSF_ONEBYTYPE | MIXER_GETLINEINFOF_SOURCE);
                if (ret != MMSYSERR_NOERROR)
                    return false;

                ctrl = Marshal.PtrToStructure<MIXERCONTROL>(ctrlHeader.pamxctrl);

                var val = new MIXERCONTROLDETAILS_UNSIGNED();
                var details = new MIXERCONTROLDETAILS
                {
                    cbStruct = Marshal.SizeOf<MIXERCONTROLDETAILS>(),
                    dwControlID = ctrl.dwControlID,
                    cChannels = 1,
                    cMultipleItems = 0,
                    paDetails = Marshal.AllocHGlobal(Marshal.SizeOf<MIXERCONTROLDETAILS_UNSIGNED>()),
                };

                try
                {
                    Marshal.StructureToPtr(val, details.paDetails, false);
                    ret = mixerGetControlDetails(hMixer, ref details, MIXER_GETCONTROLDETAILSF_VALUE);
                    if (ret == MMSYSERR_NOERROR)
                    {
                        val = Marshal.PtrToStructure<MIXERCONTROLDETAILS_UNSIGNED>(details.paDetails);
                        peakValue = (int)val.dwValue;
                        return true;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(details.paDetails);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ctrlHeader.pamxctrl);
            }
        }
        finally
        {
            mixerClose(hMixer);
        }

        return false;
    }

    private const int MIXERCONTROL_CT_CLASS_FADER = 0x60000000;

    #endregion

    #region 方法 3：waveIn 短采样的 RMS 能量分析

    private static bool TryCaptureAndAnalyze(uint deviceId, out double rmsDb)
    {
        rmsDb = -96.0;
        try
        {
            var fmt = new WavFmt
            {
                wFormatTag = 1,
                nChannels = 1,
                nSamplesPerSec = 16000,
                nAvgBytesPerSec = 32000,
                nBlockAlign = 2,
                wBitsPerSample = 16,
                cbSize = 0,
            };

            IntPtr hWave;
            int ret = WaveNative.waveInOpen(out hWave, deviceId, ref fmt,
                IntPtr.Zero, IntPtr.Zero, WaveNative.CALLBACK_NULL);
            if (ret != 0)
                return false;

            try
            {
                int bufferSize = (int)(fmt.nSamplesPerSec / 10 * fmt.nBlockAlign);
                byte[] buffer = new byte[bufferSize];
                GCHandle hBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                WAVEHDR hdr = new WAVEHDR
                {
                    lpData = hBuffer.AddrOfPinnedObject(),
                    dwBufferLength = bufferSize,
                    dwFlags = 0,
                };

                GCHandle hHdr = GCHandle.Alloc(hdr, GCHandleType.Pinned);
                ret = WaveNative.waveInPrepareHeader(hWave, ref hdr, Marshal.SizeOf<WAVEHDR>());
                if (ret != 0) { hHdr.Free(); hBuffer.Free(); return false; }

                try
                {
                    ret = WaveNative.waveInAddBuffer(hWave, ref hdr, Marshal.SizeOf<WAVEHDR>());
                    if (ret != 0) return false;

                    ret = WaveNative.waveInStart(hWave);
                    if (ret != 0) return false;

                    Thread.Sleep(80);

                    WaveNative.waveInStop(hWave);
                    WaveNative.waveInReset(hWave);
                    Thread.Sleep(20);

                    rmsDb = ComputeRms(buffer, fmt.wBitsPerSample);
                    return true;
                }
                finally
                {
                    try { WaveNative.waveInUnprepareHeader(hWave, ref hdr, Marshal.SizeOf<WAVEHDR>()); } catch { }
                    hHdr.Free();
                    hBuffer.Free();
                }
            }
            finally
            {
                WaveNative.waveInClose(hWave);
            }
        }
        catch
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEHDR
    {
        public IntPtr lpData;
        public int dwBufferLength;
        public int dwBytesRecorded;
        public IntPtr dwUser;
        public int dwFlags;
        public IntPtr dwLoops;
        public IntPtr lpNext;
        public int reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WavFmt
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    private static class WaveNative
    {
        public const uint CALLBACK_NULL = 0;

        [DllImport("winmm")]
        public static extern int waveInOpen(out IntPtr phwi, uint uDeviceID, ref WavFmt pwfx,
            IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);

        [DllImport("winmm")]
        public static extern int waveInClose(IntPtr hwi);

        [DllImport("winmm")]
        public static extern int waveInPrepareHeader(IntPtr hwi, ref WAVEHDR pwhdr, int cbwhdr);

        [DllImport("winmm")]
        public static extern int waveInUnprepareHeader(IntPtr hwi, ref WAVEHDR pwhdr, int cbwhdr);

        [DllImport("winmm")]
        public static extern int waveInAddBuffer(IntPtr hwi, ref WAVEHDR pwhdr, int cbwhdr);

        [DllImport("winmm")]
        public static extern int waveInStart(IntPtr hwi);

        [DllImport("winmm")]
        public static extern int waveInStop(IntPtr hwi);

        [DllImport("winmm")]
        public static extern int waveInReset(IntPtr hwi);
    }

    private static double ComputeRms(byte[] buffer, int bitsPerSample)
    {
        if (buffer.Length < 4) return -96.0;
        long sumSq = 0;
        int count = 0;
        if (bitsPerSample == 16)
        {
            for (int i = 0; i < buffer.Length - 1; i += 2)
            {
                short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                sumSq += (long)sample * sample;
                count++;
            }
        }
        else
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                long s = buffer[i] - 128;
                sumSq += s * s;
                count++;
            }
        }
        if (count == 0) return -96.0;
        double rms = Math.Sqrt((double)sumSq / count);
        if (rms < 1.0) return -96.0;
        double db = 20.0 * Math.Log10(rms / 32768.0);
        return Math.Max(-96.0, Math.Min(0.0, db));
    }

    #endregion

    #region 综合判定逻辑

    private DetectionResult DetectMicUsage()
    {
        var processResult = CheckAudioProcesses();
        if (processResult.InUse)
            return processResult;

        bool hasPeak = TryReadPeakMeter(_waveInDeviceId, out int peak);
        if (hasPeak && peak > 0)
        {
            return new DetectionResult
            {
                InUse = true,
                Method = "Mixer 峰值电平",
                AppName = $"峰值电平: {peak}",
            };
        }

        bool captured = TryCaptureAndAnalyze(_waveInDeviceId, out double rmsDb);
        if (captured && rmsDb > -40.0)
        {
            return new DetectionResult
            {
                InUse = true,
                Method = "音频采样 RMS",
                AppName = $"音量: {rmsDb:F1} dB",
            };
        }

        if (captured && rmsDb > -55.0)
        {
            return new DetectionResult
            {
                InUse = true,
                Method = "音频采样 (低阈值)",
                AppName = $"微弱信号: {rmsDb:F1} dB",
            };
        }

        return new DetectionResult { InUse = false, Method = "无活动" };
    }

    #endregion

    public void Dispose() => Stop();
}
