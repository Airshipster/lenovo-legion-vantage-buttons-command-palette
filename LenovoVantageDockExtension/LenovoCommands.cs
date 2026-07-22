using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace LenovoVantageDockExtension;

internal sealed partial class OpenVantageCommand : InvokableCommand
{
    public OpenVantageCommand()
    {
        Id = "codex.lenovo.vantage.open";
        Name = "Open Lenovo Vantage";
        Icon = new IconInfo("\uE8A7");
    }

    public override ICommandResult Invoke()
    {
        LenovoController.OpenVantage();
        return CommandResult.Hide();
    }
}

internal sealed partial class ShowLenovoStatusCommand : InvokableCommand
{
    public ShowLenovoStatusCommand()
    {
        Id = "codex.lenovo.vantage.status";
        Name = "Show Lenovo status";
        Icon = new IconInfo("\uE946");
    }

    public override ICommandResult Invoke()
    {
        LenovoStatus status = LenovoController.GetStatus();
        return CommandResult.ShowToast(status.ShortText);
    }
}

internal partial class SetBatteryModeCommand : InvokableCommand
{
    private readonly BatteryMode _mode;

    public SetBatteryModeCommand(BatteryMode mode, string name, string icon)
    {
        _mode = mode;
        Id = $"codex.lenovo.vantage.charge.{mode.ToString().ToLowerInvariant()}";
        Name = name;
        Icon = new IconInfo(icon);
    }

    public override ICommandResult Invoke()
    {
        try
        {
            BatteryMode actual = _mode switch
            {
                BatteryMode.Conservation => LenovoController.ToggleConservation(),
                BatteryMode.RapidCharge => LenovoController.ToggleRapidCharge(),
                _ => BatteryMode.Normal,
            };
            LenovoVantageDockBand.RefreshActive();
            string suffix = actual == _mode ? "enabled" : actual == BatteryMode.Normal ? "disabled" : $"requested, current: {actual}";
            return CommandResult.ShowToast($"Charging mode {suffix}");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Charging mode was not changed: {ex.Message}");
        }
    }
}

internal sealed partial class ToggleConservationCommand : SetBatteryModeCommand
{
    public ToggleConservationCommand()
        : base(BatteryMode.Conservation, "Battery conservation mode", "\uEBAE")
    {
    }
}

internal sealed partial class ToggleRapidChargeCommand : SetBatteryModeCommand
{
    public ToggleRapidChargeCommand()
        : base(BatteryMode.RapidCharge, "Rapid charge", "\uE945")
    {
    }
}

internal sealed partial class SetHybridGpuModeCommand : InvokableCommand
{
    private readonly GpuMode _mode;

    public SetHybridGpuModeCommand(GpuMode mode, string name)
    {
        _mode = mode;
        Id = $"codex.lenovo.vantage.gpu.{mode.ToString().ToLowerInvariant()}";
        Name = name;
        Icon = new IconInfo("\uE7F4");
    }

    public override ICommandResult Invoke()
    {
        try
        {
            bool ok = LenovoController.SetHybridGpuMode(_mode);
            LenovoVantageDockBand.RefreshActive();
            if (_mode == GpuMode.DGpu)
            {
                return CommandResult.ShowToast(ok
                    ? "dGPU mode selected. A restart may be required for the change to take effect."
                    : "dGPU mode requested. A restart may be required.");
            }

            return CommandResult.ShowToast(ok ? $"GPU mode enabled: {Name}" : $"GPU mode requested: {Name}");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"GPU mode was not changed: {ex.Message}");
        }
    }
}

internal sealed partial class NotReadyCommand : InvokableCommand
{
    private readonly string _message;

    public NotReadyCommand(string name, string message, string icon)
    {
        Id = $"codex.lenovo.vantage.pending.{name.ToLowerInvariant().Replace(' ', '.')}";
        Name = name;
        Icon = new IconInfo(icon);
        _message = message;
    }

    public override ICommandResult Invoke()
    {
        return CommandResult.ShowToast(_message);
    }
}
