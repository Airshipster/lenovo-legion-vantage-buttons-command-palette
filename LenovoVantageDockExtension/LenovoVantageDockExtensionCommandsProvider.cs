// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace LenovoVantageDockExtension;

public partial class LenovoVantageDockExtensionCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public LenovoVantageDockExtensionCommandsProvider()
    {
        Id = "codex.lenovo.vantage.dock";
        DisplayName = "Lenovo Vantage Dock";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _commands = [
            new CommandItem(new LenovoVantageDockExtensionPage()) { Title = DisplayName },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override ICommandItem[] GetDockBands()
    {
        return [
            new CommandItem(new LenovoVantageDockBand())
            {
                Title = "Lenovo Vantage",
                Icon = new IconInfo("\uE7F8"),
            },
        ];
    }
}
