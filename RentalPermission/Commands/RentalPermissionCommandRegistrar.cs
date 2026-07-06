using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace RentalPermission;

internal static class RentalPermissionCommandRegistrar
{
    public static void Register(ICoreServerAPI api, RentalPermissionCommandHandlers handlers)
    {
        api.ChatCommands.Create("rentalpermission")
            .WithAlias("rentperm")
            .WithDescription("Manage RentalPermission")
            .RequiresPrivilege(Privilege.chat)
            .BeginSubCommand("reload")
                .WithDescription("Reload rentalpermission.json")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(handlers.Reload)
                .EndSubCommand()
            .BeginSubCommand("list")
                .WithDescription("List active rental records")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(handlers.List)
                .EndSubCommand()
            .BeginSubCommand("mine")
                .WithDescription("List your rental records")
                .HandleWith(handlers.Mine)
                .EndSubCommand()
            .BeginSubCommand("renew")
                .WithDescription("Renew one of your rentals")
                .WithArgs(api.ChatCommands.Parsers.Word("rental id"))
                .HandleWith(handlers.Renew)
                .EndSubCommand()
            .BeginSubCommand("cancel")
                .WithDescription("Cancel a rental record without touching the block")
                .WithArgs(api.ChatCommands.Parsers.Word("rental id"))
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(handlers.Cancel)
                .EndSubCommand()
            .BeginSubCommand("process")
                .WithDescription("Process expired rentals now")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(handlers.Process)
                .EndSubCommand()
            .BeginSubCommand("here")
                .WithDescription("Inspect rental claim matching at your current position")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(handlers.Here)
                .EndSubCommand();
    }
}
