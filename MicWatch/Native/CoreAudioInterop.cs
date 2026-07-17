using System.Runtime.InteropServices;

namespace MicWatch.Native;

#region 枚举与结构

[Flags]
internal enum CLSCTX : uint
{
    INPROC_SERVER = 0x1,
    INPROC_HANDLER = 0x2,
    LOCAL_SERVER = 0x4,
    REMOTE_SERVER = 0x10,
    ALL = INPROC_SERVER | INPROC_HANDLER | LOCAL_SERVER | REMOTE_SERVER,
}

[Flags]
internal enum COINIT : uint
{
    COINIT_MULTITHREADED = 0x0,
    COINIT_APARTMENTTHREADED = 0x2,
    COINIT_DISABLE_OLE1DDE = 0x4,
    COINIT_SPEED_OVER_MEMORY = 0x8,
}

internal enum EDataFlow : uint
{
    eRender = 0,
    eCapture = 1,
    eAll = 2,
}

internal enum ERole : uint
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2,
}

[Flags]
internal enum DeviceState : uint
{
    DEVICE_STATE_ACTIVE = 0x1,
    DEVICE_STATE_DISABLED = 0x2,
    DEVICE_STATE_NOTPRESENT = 0x4,
    DEVICE_STATE_UNPLUGGED = 0x8,
}

internal enum AudioSessionState : uint
{
    AudioSessionStateInactive = 0,
    AudioSessionStateActive = 1,
    AudioSessionStateExpired = 2,
}

internal enum STGM : uint
{
    READ = 0,
}

[StructLayout(LayoutKind.Sequential)]
internal struct PROPERTYKEY
{
    public Guid fmtid;
    public int pid;
}


[StructLayout(LayoutKind.Sequential)]
internal struct PROPVARIANT
{
    public ushort vt;
    public ushort wReserved1;
    public ushort wReserved2;
    public ushort wReserved3;
    public IntPtr pwszVal;
}

#endregion

#region COM 接口 (vtable 顺序严格匹配 Windows SDK 头文件)

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
internal interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(EDataFlow dataFlow, DeviceState dwStateMask, out IMMDeviceCollection ppDevices);
    void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
    void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
    void RegisterEndpointNotificationCallback(IntPtr pClient);
    void UnregisterEndpointNotificationCallback(IntPtr pClient);
}

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("0BD7A1BE-7A1A-44DB-8397-CC3A63B51E8F")]
internal interface IMMDeviceCollection
{
    void GetCount(out uint pcDevices);
    void Item(uint nDevice, out IMMDevice ppDevice);
}

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
internal interface IMMDevice
{
    void Activate([MarshalAs(UnmanagedType.LPStruct)] Guid iid, CLSCTX dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    void OpenPropertyStore(STGM stgmAccess, out IPropertyStore ppProperties);
    void GetId(out IntPtr ppstrId);
    void GetState(out uint pdwState);
}

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("886D8EEB-8A1A-40C8-9248-B3B2E6C9E6DB")]
internal interface IPropertyStore
{
    void GetCount(out uint cProps);
    void GetAt(uint iProp, out PROPERTYKEY pkey);
    void GetValue([MarshalAs(UnmanagedType.LPStruct)] PROPERTYKEY key, out PROPVARIANT pv);
    void SetValue([MarshalAs(UnmanagedType.LPStruct)] PROPERTYKEY key, PROPVARIANT pv);
    void Commit();
}

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("77AA99A0-1C3B-484B-ADFE-ACCC8BFE171C")]
internal interface IAudioSessionManager2
{
    void GetSessionEnumerator(out IAudioSessionEnumerator ppSessionEnum);
    void RegisterSessionNotification(IntPtr pSessionNotification);
    void UnregisterSessionNotification(IntPtr pSessionNotification);
    void RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionID, IntPtr duckNotification);
    void UnregisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionID, IntPtr duckNotification);
}

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
internal interface IAudioSessionEnumerator
{
    void GetCount(out int SessionCount);
    void GetSession(int SessionCount, out IAudioSessionControl Session);
}

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
internal interface IAudioSessionControl
{
    void GetState(out AudioSessionState pRetVal);
    void GetDisplayName(out IntPtr pRetVal);
    void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, IntPtr EventContext);
    void GetIconPath(out IntPtr pRetVal);
    void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, IntPtr EventContext);
    void GetGroupingParam(out Guid pRetVal);
    void SetGroupingParam(ref Guid Override, ref Guid EventContext);
    void RegisterAudioSessionNotification(IntPtr NewNotifications);
    void UnregisterAudioSessionNotification(IntPtr NewNotifications);
}


[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
internal interface IAudioSessionControl2 : IAudioSessionControl
{
    void GetSessionIdentifier(out IntPtr pRetVal);
    void GetSessionInstanceIdentifier(out IntPtr pRetVal);
    void GetProcessId(out uint pRetVal);
    int IsSystemSoundsSession();
    void SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
}

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("00000001-0000-0000-C000-000000000046")]
internal interface IClassFactory
{
    void CreateInstance(IntPtr pUnkOuter, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    void LockServer(bool fLock);
}

#endregion

#region 常量与工具

internal static class CoreAudioGuids
{
    public static readonly Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692B");
    public static readonly Guid IID_IMMDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    public static readonly Guid IID_IAudioSessionManager2 = new("77AA99A0-1C3B-484B-ADFE-ACCC8BFE171C");
    public static readonly Guid IID_IClassFactory = new("00000001-0000-0000-C000-000000000046");

    public static readonly PROPERTYKEY PKEY_Device_FriendlyName = new()
    {
        fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
        pid = 14,
    };
}

internal delegate int DllGetClassObjectDelegate(
    [MarshalAs(UnmanagedType.LPStruct)] Guid clsid,
    [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
    out IntPtr ppv);

internal static class CoreAudioNative
{
    private const int S_OK = 0;
    private const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);

    [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int CoCreateInstance(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
        IntPtr pUnkOuter,
        CLSCTX dwClsCtx,
        [MarshalAs(UnmanagedType.LPStruct)] Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("ole32.dll")]
    public static extern int PropVariantClear(ref PROPVARIANT pvar);

    [DllImport("ole32.dll")]
    public static extern int CoInitializeEx(IntPtr pvReserved, COINIT dwCoInit);

    [DllImport("ole32.dll")]
    public static extern void CoUninitialize();

    [DllImport("ole32.dll")]
    public static extern void CoTaskMemFree(IntPtr pv);

    internal static bool IsClassNotFound(int hr) => hr == REGDB_E_CLASSNOTREG;

    private static IntPtr? _mmdevapiHandle;
    private static DllGetClassObjectDelegate? _dllGetClassObj;

    internal static int CreateMMDeviceEnumerator(out IMMDeviceEnumerator enumerator)
    {
        enumerator = null!;
        int hr;

        hr = TryCreateViaDllGetClassObject(out enumerator);
        if (hr >= 0 && enumerator is not null) return hr;

        hr = TryCreateViaCoCreateInstance(out enumerator);
        if (hr >= 0 && enumerator is not null) return hr;

        return hr;
    }

    private static int TryCreateViaDllGetClassObject(out IMMDeviceEnumerator enumerator)
    {
        enumerator = null!;
        try
        {
            if (_mmdevapiHandle == IntPtr.Zero || _mmdevapiHandle == new IntPtr(-1))
            {
                _mmdevapiHandle = LoadLibrary("mmdevapi.dll");
                if (_mmdevapiHandle == IntPtr.Zero || _mmdevapiHandle == new IntPtr(-1))
                    return REGDB_E_CLASSNOTREG;

                ptrProc = GetProcAddress(_mmdevapiHandle.Value, "DllGetClassObject");
                if (ptrProc == IntPtr.Zero)
                    return REGDB_E_CLASSNOTREG;

                _dllGetClassObj = Marshal.GetDelegateForFunctionPointer<DllGetClassObjectDelegate>(ptrProc);
            }

            IntPtr pClassFactory;
            var dlg = _dllGetClassObj;
            if (dlg is null) return REGDB_E_CLASSNOTREG;

            int hr = dlg(
                CoreAudioGuids.CLSID_MMDeviceEnumerator,
                CoreAudioGuids.IID_IClassFactory,
                out pClassFactory);

            if (hr < 0 || pClassFactory == IntPtr.Zero) return hr;

            var factory = (IClassFactory)Marshal.GetObjectForIUnknown(pClassFactory);
            object obj;
            factory.CreateInstance(
                IntPtr.Zero,
                CoreAudioGuids.IID_IMMDeviceEnumerator,
                out obj);

            if (obj is IMMDeviceEnumerator devEnum)
            {
                enumerator = devEnum;
                return S_OK;
            }

            Marshal.Release(pClassFactory);
            return unchecked((int)0x80004002);
        }
        catch
        {
            return REGDB_E_CLASSNOTREG;
        }
    }
    private static IntPtr ptrProc;

    private static int TryCreateViaCoCreateInstance(out IMMDeviceEnumerator enumerator)
    {
        enumerator = null!;
        try
        {
            int hr = CoCreateInstance(
                CoreAudioGuids.CLSID_MMDeviceEnumerator,
                IntPtr.Zero,
                CLSCTX.INPROC_SERVER,
                CoreAudioGuids.IID_IMMDeviceEnumerator,
                out object obj);
            if (hr >= 0 && obj is IMMDeviceEnumerator devEnum)
                enumerator = devEnum;
            return hr;
        }
        catch
        {
            return REGDB_E_CLASSNOTREG;
        }
    }
}
#endregion
