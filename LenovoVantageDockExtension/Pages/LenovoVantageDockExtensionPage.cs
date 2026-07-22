// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace LenovoVantageDockExtension;

internal sealed partial class LenovoVantageDockExtensionPage : ListPage
{
    public LenovoVantageDockExtensionPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Lenovo Vantage Dock";
        Name = "Open";
    }

    public override IListItem[] GetItems()
    {
        string statusText;
        try
        {
            statusText = LenovoController.GetStatus().ShortText;
        }
        catch
        {
            statusText = "Status unavailable";
        }

        return
        [
            new ListItem(new ShowLenovoStatusCommand())
            {
                Title = "Status",
                Subtitle = statusText,
                Icon = new IconInfo("\uE946"),
            },
            new ListItem(new OpenVantageCommand())
            {
                Title = "Open Lenovo Vantage",
                Subtitle = "Launch the Lenovo Vantage app",
                Icon = new IconInfo("\uE8A7"),
            },
            new ListItem(new ToggleConservationCommand())
            {
                Title = "Toggle conservation mode",
                Subtitle = "Switch conservation charging on/off",
                Icon = new IconInfo("\uEBAE"),
            },
            new ListItem(new ToggleRapidChargeCommand())
            {
                Title = "Toggle rapid charge",
                Subtitle = "Switch rapid charge on/off",
                Icon = new IconInfo("\uE945"),
            },
            new ListItem(new SetHybridGpuModeCommand(GpuMode.Hybrid, "Hybrid"))
            {
                Title = "GPU mode: Hybrid",
                Subtitle = "Switchable graphics",
                Icon = new IconInfo("\uE7F4"),
            },
            new ListItem(new SetHybridGpuModeCommand(GpuMode.IGpuOnly, "iGPU only"))
            {
                Title = "GPU mode: iGPU only",
                Subtitle = "Use Intel graphics only",
                Icon = new IconInfo("\uE7F4"),
            },
            new ListItem(new SetHybridGpuModeCommand(GpuMode.HybridAuto, "Hybrid auto"))
            {
                Title = "GPU mode: Hybrid auto",
                Subtitle = "Lenovo automatic hybrid graphics",
                Icon = new IconInfo("\uE7F4"),
            },
        ];
    }
}
