using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace LenovoVantageDockExtension;

internal sealed partial class LenovoStatusPage : ListPage
{
    public LenovoStatusPage()
    {
        Id = "codex.lenovo.vantage.status.page";
        Name = "Lenovo";
        Title = "Lenovo Vantage";
        Icon = new IconInfo("\uE946");
    }

    public override IListItem[] GetItems()
    {
        LenovoController.TryGetStatus(out LenovoStatus status);
        bool hasBattery = LenovoController.TryGetBatteryMode(out BatteryMode batteryMode);
        GpuMode? gpuMode = status.IGpuModeStatus is null ? null : (GpuMode)status.IGpuModeStatus.Value;

        return
        [
            new ListItem(new OpenVantageCommand())
            {
                Title = "Open Lenovo Vantage",
                Subtitle = "Open the full Lenovo settings app",
                Icon = new IconInfo("\uE8A7"),
                Section = "App",
            },
            ModeItem(new ToggleConservationCommand(), "Battery conservation mode", batteryMode == BatteryMode.Conservation && hasBattery, "Charging", "enabled"),
            ModeItem(new ToggleRapidChargeCommand(), "Rapid charge", batteryMode == BatteryMode.RapidCharge && hasBattery, "Charging", "enabled"),
            ModeItem(new SetHybridGpuModeCommand(GpuMode.Hybrid, "Hybrid mode"), "Hybrid mode", gpuMode == GpuMode.Hybrid, "GPU mode", "Intel + NVIDIA"),
            ModeItem(new SetHybridGpuModeCommand(GpuMode.IGpuOnly, "Hybrid-iGPU only mode"), "Hybrid-iGPU only mode", gpuMode == GpuMode.IGpuOnly, "GPU mode", "Intel"),
            ModeItem(new SetHybridGpuModeCommand(GpuMode.HybridAuto, "Hybrid-Auto mode"), "Hybrid-Auto mode", gpuMode == GpuMode.HybridAuto, "GPU mode", GpuSummary(status)),
            ModeItem(new SetHybridGpuModeCommand(GpuMode.DGpu, "dGPU mode"), "dGPU mode", gpuMode == GpuMode.DGpu, "GPU mode", "NVIDIA"),
        ];
    }

    private static ListItem ModeItem(ICommand command, string title, bool active, string section, string subtitle)
    {
        return new ListItem(command)
        {
            Title = title,
            Subtitle = active ? $"Current mode: {subtitle}" : string.Empty,
            Icon = new IconInfo(active ? "\uE73E" : command.Icon?.Dark.Icon ?? string.Empty),
            Section = section,
        };
    }

    private static string GpuSummary(LenovoStatus status)
    {
        return status.NvidiaRuntimeText.Contains("active", System.StringComparison.OrdinalIgnoreCase)
            ? "Intel + NVIDIA"
            : "Intel; NVIDIA asleep";
    }
}
