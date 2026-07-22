using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace LenovoVantageDockExtension;

internal sealed partial class LenovoVantageDockBand : ListPage
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private static LenovoVantageDockBand? activeBand;
    private static readonly object RefreshLock = new();
    private static Timer? refreshTimer;
    private static int refreshInProgress;
    private static DockSnapshot? lastSnapshot;
    private IListItem[] items = [];

    public LenovoVantageDockBand()
    {
        activeBand = this;
        Id = "codex.lenovo.vantage.dock.band";
        Name = "Lenovo Vantage";
        Title = "Lenovo Vantage";
        Icon = new IconInfo("\uE7F8");
        RefreshItems(force: true);
        refreshTimer ??= new Timer(_ => RefreshActiveFromTimer(), null, RefreshInterval, RefreshInterval);
        refreshTimer.Change(RefreshInterval, RefreshInterval);
    }

    public static void RefreshActive()
    {
        activeBand?.RefreshItems(force: true);
    }

    private static void RefreshActiveFromTimer()
    {
        if (Interlocked.Exchange(ref refreshInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            activeBand?.RefreshItems(force: false);
        }
        finally
        {
            Volatile.Write(ref refreshInProgress, 0);
        }
    }

    private void RefreshItems(bool force)
    {
        DockSnapshot snapshot = ReadSnapshot();
        lock (RefreshLock)
        {
            if (!force && snapshot == lastSnapshot)
            {
                return;
            }

            lastSnapshot = snapshot;
            items = BuildItems(snapshot);
            RaiseItemsChanged(items.Length);
        }
    }

    public override IListItem[] GetItems()
    {
        return items;
    }

    private static DockSnapshot ReadSnapshot()
    {
        LenovoController.TryGetStatus(out LenovoStatus status, includeNvidiaRuntime: false);
        LenovoController.TryGetBatteryMode(out BatteryMode batteryMode);
        return new DockSnapshot(batteryMode, status.IGpuModeStatus, status.ShortText);
    }

    private static IListItem[] BuildItems(DockSnapshot snapshot)
    {
        GpuMode? gpuMode = snapshot.GpuModeStatus is null ? null : (GpuMode)snapshot.GpuModeStatus.Value;

        return
        [
            new ListItem(new LenovoStatusPage())
            {
                Title = "Status",
                Icon = new IconInfo("\uE946"),
                Subtitle = snapshot.ShortText,
            },
            new ListItem(new ToggleConservationCommand())
            {
                Title = "Eco",
                Icon = new IconInfo(snapshot.BatteryMode == BatteryMode.Conservation ? "\uE73E" : "\uEBAE"),
                Subtitle = snapshot.BatteryMode == BatteryMode.Conservation ? "Active: battery conservation" : "Battery conservation mode",
            },
            new ListItem(new ToggleRapidChargeCommand())
            {
                Title = "Fast",
                Icon = new IconInfo(snapshot.BatteryMode == BatteryMode.RapidCharge ? "\uE73E" : "\uE945"),
                Subtitle = snapshot.BatteryMode == BatteryMode.RapidCharge ? "Active: rapid charge" : "Rapid charge",
            },
            new ListItem(new SetHybridGpuModeCommand(GpuMode.IGpuOnly, "iGPU"))
            {
                Title = "iGPU",
                Icon = new IconInfo(gpuMode == GpuMode.IGpuOnly ? "\uE73E" : "\uE7F4"),
                Subtitle = gpuMode == GpuMode.IGpuOnly ? "Active" : "Hybrid-iGPU only mode",
            },
            new ListItem(new SetHybridGpuModeCommand(GpuMode.HybridAuto, "Auto"))
            {
                Title = "Auto",
                Icon = new IconInfo(gpuMode == GpuMode.HybridAuto ? "\uE73E" : "\uE7F4"),
                Subtitle = gpuMode == GpuMode.HybridAuto ? "Active" : "Hybrid-Auto mode",
            },
        ];
    }

    private sealed record DockSnapshot(BatteryMode BatteryMode, uint? GpuModeStatus, string ShortText);
}
