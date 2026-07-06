using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class SystemRentalState
{
    private readonly HashSet<string> bypassSystemRentals = new();

    public bool ConsumeBypass(IServerPlayer player, BlockPos pos, RentalActionType actionType)
    {
        return bypassSystemRentals.Remove(Key(player, pos, actionType));
    }

    public void AddBypass(IServerPlayer player, BlockPos pos, RentalActionType actionType)
    {
        bypassSystemRentals.Add(Key(player, pos, actionType));
    }

    public void RemoveBypass(IServerPlayer player, BlockPos pos, RentalActionType actionType)
    {
        bypassSystemRentals.Remove(Key(player, pos, actionType));
    }

    public static string Key(IServerPlayer player, BlockPos pos, RentalActionType actionType)
    {
        return $"{player.PlayerUID}|{actionType}|{pos.dimension}/{pos.X}/{pos.Y}/{pos.Z}";
    }
}
