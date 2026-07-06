using Vintagestory.API.Common;

namespace RentalPermission;

internal sealed class RentalPermissionCommandHandlers
{
    public OnCommandDelegate Reload { get; init; } = _ => TextCommandResult.Error("Command handler not configured.");

    public OnCommandDelegate List { get; init; } = _ => TextCommandResult.Error("Command handler not configured.");

    public OnCommandDelegate Mine { get; init; } = _ => TextCommandResult.Error("Command handler not configured.");

    public OnCommandDelegate Renew { get; init; } = _ => TextCommandResult.Error("Command handler not configured.");

    public OnCommandDelegate Cancel { get; init; } = _ => TextCommandResult.Error("Command handler not configured.");

    public OnCommandDelegate Process { get; init; } = _ => TextCommandResult.Error("Command handler not configured.");

    public OnCommandDelegate Here { get; init; } = _ => TextCommandResult.Error("Command handler not configured.");
}
