using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace LenovoVantageDockExtension;

internal sealed record LenovoStatus(
    uint? PowerChargeMode,
    uint? IGpuSupport,
    uint? IGpuModeStatus,
    uint? ThermalMode,
    uint? SmartFanMode,
    string NvidiaRuntimeText)
{
    public string ChargeText => PowerChargeMode switch
    {
        null => "Charge: unavailable",
        _ when (PowerChargeMode.Value & 0x20) != 0 => "Charge: conservation",
        _ when (PowerChargeMode.Value & 0x04) != 0 => "Charge: rapid",
        _ => "Charge: normal",
    };

    public string GpuText => IGpuModeStatus switch
    {
        0 => "GPU: hybrid",
        1 => "GPU: iGPU only",
        2 => "GPU: hybrid auto",
        3 => "GPU: dGPU",
        null => "GPU: unavailable",
        _ => $"GPU: mode {IGpuModeStatus}",
    };

    public string ShortText => $"{ChargeText}; {GpuText}";
}

internal static class LenovoController
{
    private const string ScopePath = @"root\WMI";
    private const string GameZoneClass = "LENOVO_GAMEZONE_DATA";
    private const string BatteryModeRegistryPath = @"Software\Lenovo\VantageService\AddinData\IdeaNotebookAddin";
    private const uint IoctlEnergyBatteryChargeMode = 0x831020F8;
    private static readonly object GpuModeLock = new();

    public static LenovoStatus GetStatus(bool includeNvidiaRuntime = true)
    {
        uint? iGpuSupport = TryGetGameZoneValue("IsSupportIGPUMode");
        uint? iGpuModeStatus = TryGetGameZoneValue("GetIGPUModeStatus");
        if (iGpuModeStatus is null)
        {
            iGpuModeStatus = GetGameZoneValueViaPowerShell("GetIGPUModeStatus");
        }

        if (iGpuSupport is null)
        {
            iGpuSupport = GetGameZoneValueViaPowerShell("IsSupportIGPUMode");
        }

        return new LenovoStatus(
            TryGetGameZoneValue("GetPowerChargeMode"),
            iGpuSupport,
            iGpuModeStatus,
            TryGetGameZoneValue("GetThermalMode"),
            TryGetGameZoneValue("GetSmartFanMode"),
            includeNvidiaRuntime ? GetNvidiaRuntimeText() : "NVIDIA: not checked");
    }

    public static void OpenVantage()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = @"shell:AppsFolder\E046963F.LenovoCompanion_k1h2ywk1493x8!App",
            UseShellExecute = true,
        });
    }

    public static bool TryGetStatus(out LenovoStatus status, bool includeNvidiaRuntime = true)
    {
        try
        {
            status = GetStatus(includeNvidiaRuntime);
            return true;
        }
        catch
        {
            status = new LenovoStatus(null, null, null, null, null, "NVIDIA: unavailable");
            return false;
        }
    }

    public static bool TryGetBatteryMode(out BatteryMode mode)
    {
        try
        {
            mode = GetBatteryMode();
            return true;
        }
        catch
        {
            mode = BatteryMode.Normal;
            return false;
        }
    }

    public static BatteryMode GetBatteryMode()
    {
        uint raw = SendEnergyDriverCode(0xFF);
        if ((raw & 0x20) != 0)
        {
            return BatteryMode.Conservation;
        }

        if ((raw & 0x04) != 0)
        {
            return BatteryMode.RapidCharge;
        }

        return BatteryMode.Normal;
    }

    public static BatteryMode ToggleConservation()
    {
        BatteryMode next = GetBatteryMode() == BatteryMode.Conservation
            ? BatteryMode.Normal
            : BatteryMode.Conservation;
        SetBatteryMode(next);
        return GetBatteryMode();
    }

    public static BatteryMode ToggleRapidCharge()
    {
        BatteryMode next = GetBatteryMode() == BatteryMode.RapidCharge
            ? BatteryMode.Normal
            : BatteryMode.RapidCharge;
        SetBatteryMode(next);
        return GetBatteryMode();
    }

    public static void SetBatteryMode(BatteryMode mode)
    {
        uint[] codes = mode switch
        {
            BatteryMode.Conservation => [0x08, 0x03],
            BatteryMode.Normal => [0x05, 0x08],
            BatteryMode.RapidCharge => [0x05, 0x07],
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

        foreach (uint code in codes)
        {
            SendEnergyDriverCode(code);
        }

        string registryValue = mode switch
        {
            BatteryMode.Conservation => "Storage",
            BatteryMode.Normal => "Normal",
            BatteryMode.RapidCharge => "Quick",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
        Registry.CurrentUser.CreateSubKey(BatteryModeRegistryPath)?.SetValue("BatteryChargeMode", registryValue);
    }

    public static bool SetHybridGpuMode(GpuMode mode)
    {
        lock (GpuModeLock)
        {
            try
            {
                if (!SetGameZoneValue("SetIGPUModeStatus", (uint)mode))
                {
                    SetGameZoneValueViaPowerShell("SetIGPUModeStatus", (uint)mode);
                }
            }
            catch
            {
                // Some Lenovo BIOS/WMI builds apply the mode but still throw from the
                // provider. Verify below before deciding whether this really failed.
                SetGameZoneValueViaPowerShell("SetIGPUModeStatus", (uint)mode);
            }

            return WaitForGpuMode(mode);
        }
    }

    private static uint? GetGameZoneValue(string methodName)
    {
        return WithGameZoneInstance<uint?>(instance =>
        {
            using ManagementBaseObject result = instance.InvokeMethod(methodName, null, null);
            if (result["ReturnValue"] is not bool ok || !ok)
            {
                return null;
            }

            return result["Data"] switch
            {
                uint value => value,
                int value => unchecked((uint)value),
                _ => null,
            };
        });
    }

    private static uint? TryGetGameZoneValue(string methodName)
    {
        try
        {
            return GetGameZoneValue(methodName);
        }
        catch
        {
            return null;
        }
    }

    private static bool SetGameZoneValue(string methodName, uint mode)
    {
        return WithGameZoneInstance<bool>(instance =>
        {
            using ManagementBaseObject input = instance.GetMethodParameters(methodName);
            input["mode"] = mode;
            using ManagementBaseObject result = instance.InvokeMethod(methodName, input, null);
            return result["ReturnValue"] is bool ok && ok;
        }) == true;
    }

    private static uint? GetGameZoneValueViaPowerShell(string methodName)
    {
        string script = "$i=Get-CimInstance -Namespace root\\WMI -ClassName LENOVO_GAMEZONE_DATA -ErrorAction Stop | Select-Object -First 1; " +
            $"$r=Invoke-CimMethod -InputObject $i -MethodName {methodName} -ErrorAction Stop; " +
            "if($r.ReturnValue){[Console]::Out.Write($r.Data)}";
        string output = RunHiddenPowerShell(script);
        return uint.TryParse(output, out uint value) ? value : null;
    }

    private static bool SetGameZoneValueViaPowerShell(string methodName, uint mode)
    {
        string script = "$i=Get-CimInstance -Namespace root\\WMI -ClassName LENOVO_GAMEZONE_DATA -ErrorAction Stop | Select-Object -First 1; " +
            $"$r=Invoke-CimMethod -InputObject $i -MethodName {methodName} -Arguments @{{mode={mode}}} -ErrorAction Stop; " +
            "if($r.ReturnValue){[Console]::Out.Write('OK')}";
        return string.Equals(RunHiddenPowerShell(script), "OK", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetNvidiaRuntimeText()
    {
        const string script =
            "$d=Get-PnpDevice -Class Display -ErrorAction SilentlyContinue | Where-Object {$_.FriendlyName -like '*NVIDIA*'} | Select-Object -First 1; " +
            "if($null -eq $d){[Console]::Out.Write('NVIDIA: unavailable'); exit}; " +
            "if($d.Status -eq 'OK'){[Console]::Out.Write('NVIDIA: active')} " +
            "elseif($d.Problem -eq 'CM_PROB_PHANTOM'){[Console]::Out.Write('NVIDIA: asleep')} " +
            "else{[Console]::Out.Write(('NVIDIA: ' + $d.Status))}";

        string output = RunHiddenPowerShell(script);
        return string.IsNullOrWhiteSpace(output) ? "NVIDIA: unavailable" : output;
    }

    private static bool WaitForGpuMode(GpuMode mode)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(6);
        do
        {
            uint? actual = TryGetGameZoneValue("GetIGPUModeStatus") ?? GetGameZoneValueViaPowerShell("GetIGPUModeStatus");
            if (actual == (uint)mode)
            {
                return true;
            }

            System.Threading.Thread.Sleep(350);
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    private static string RunHiddenPowerShell(string script)
    {
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " +
                Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Unable to start PowerShell.");

        string output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit(5000);
        return output;
    }

    private static T? WithGameZoneInstance<T>(Func<ManagementObject, T> action)
    {
        using ManagementClass cls = new(ScopePath, GameZoneClass, null);
        using ManagementObjectCollection instances = cls.GetInstances();
        foreach (ManagementObject instance in instances)
        {
            using (instance)
            {
                return action(instance);
            }
        }

        return default;
    }

    private static uint SendEnergyDriverCode(uint code)
    {
        using SafeFileHandle handle = NativeMethods.CreateFile(
            @"\\.\EnergyDrv",
            NativeMethods.GenericRead | NativeMethods.GenericWrite,
            NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
            IntPtr.Zero,
            NativeMethods.OpenExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            throw new InvalidOperationException($"Unable to open Lenovo EnergyDrv. Win32={Marshal.GetLastWin32Error()}");
        }

        uint input = code;
        bool ok = NativeMethods.DeviceIoControl(
            handle,
            IoctlEnergyBatteryChargeMode,
            ref input,
            sizeof(uint),
            out uint output,
            sizeof(uint),
            out _,
            IntPtr.Zero);

        if (!ok)
        {
            throw new InvalidOperationException($"Lenovo EnergyDrv call failed. Win32={Marshal.GetLastWin32Error()}");
        }

        return output;
    }

    private static class NativeMethods
    {
        public const uint GenericRead = 0x80000000;
        public const uint GenericWrite = 0x40000000;
        public const uint FileShareRead = 1;
        public const uint FileShareWrite = 2;
        public const uint OpenExisting = 3;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            ref uint lpInBuffer,
            uint nInBufferSize,
            out uint lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);
    }
}

internal enum BatteryMode
{
    Normal,
    Conservation,
    RapidCharge,
}

internal enum GpuMode : uint
{
    Hybrid = 0,
    IGpuOnly = 1,
    HybridAuto = 2,
    DGpu = 3,
}
