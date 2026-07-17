using Microsoft.Win32;

namespace MicWatch.Helpers;

public enum MicAccessStatus
{
    Unknown,
    Allowed,
    Denied
}

public static class MicPermissionHelper
{
    private const string ConsentStoreRoot =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    public static MicAccessStatus GetStatus()
    {
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(ConsentStoreRoot, false);
            if (root is null)
                return MicAccessStatus.Unknown;

            var nonPackaged = root.OpenSubKey("NonPackaged", false);
            if (nonPackaged is not null)
            {
                var v = nonPackaged.GetValue("Value") as string;
                if (!string.IsNullOrEmpty(v))
                    return v.Equals("Deny", StringComparison.OrdinalIgnoreCase)
                        ? MicAccessStatus.Denied
                        : MicAccessStatus.Allowed;
            }

            var global = root.GetValue("Value") as string;
            if (!string.IsNullOrEmpty(global))
                return global.Equals("Deny", StringComparison.OrdinalIgnoreCase)
                    ? MicAccessStatus.Denied
                    : MicAccessStatus.Allowed;

            return MicAccessStatus.Unknown;
        }
        catch
        {
            return MicAccessStatus.Unknown;
        }
    }

    public static string GetStatusText()
    {
        return GetStatus() switch
        {
            MicAccessStatus.Allowed => "已允许（Windows 麦克风权限正常）",
            MicAccessStatus.Denied => "已被禁用（Windows 已禁止应用访问麦克风，请在 设置 → 隐私和安全性 → 麦克风 中开启“允许桌面应用访问你的麦克风”）",
            _ => "无法读取（可能无法访问注册表，或无明确策略）"
        };
    }
}
