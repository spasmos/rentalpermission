using HarmonyLib;
using System.Reflection;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RentalPermission;

internal sealed class RentalExpirationActionExecutor
{
    private readonly Func<ICoreServerAPI?> getServerApi;
    private readonly MarketStallResetService marketStallResetService;
    private readonly Func<RentalRecord, string> getRentalDisplayName;
    private readonly Action<RentalRecord, string, object[]> notifyRentalOwner;

    public RentalExpirationActionExecutor(
        Func<ICoreServerAPI?> getServerApi,
        MarketStallResetService marketStallResetService,
        Func<RentalRecord, string> getRentalDisplayName,
        Action<RentalRecord, string, object[]> notifyRentalOwner)
    {
        this.getServerApi = getServerApi;
        this.marketStallResetService = marketStallResetService;
        this.getRentalDisplayName = getRentalDisplayName;
        this.notifyRentalOwner = notifyRentalOwner;
    }

    public string Execute(RentalRecord record)
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null)
        {
            return "server API unavailable";
        }

        string action = string.IsNullOrWhiteSpace(record.OnExpired) ? "WarnOnly" : record.OnExpired;
        BlockPos pos = new BlockPos(record.X, record.Y, record.Z, record.Dimension);

        notifyRentalOwner(record, "rentalpermission-expired", new object[] { getRentalDisplayName(record), record.PositionKey, action });

        if (action.Equals("DoNothing", StringComparison.OrdinalIgnoreCase)
            || action.Equals("WarnOnly", StringComparison.OrdinalIgnoreCase))
        {
            return action;
        }

        ModSystemBlockReinforcement? reinforcement = sapi.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
        if (reinforcement == null)
        {
            return "block reinforcement system unavailable";
        }

        if (action.Equals("UnlockOnly", StringComparison.OrdinalIgnoreCase))
        {
            return TryUnlockOnly(reinforcement, pos);
        }

        if (action.Equals("RemoveReinforcement", StringComparison.OrdinalIgnoreCase))
        {
            reinforcement.ClearReinforcement(pos);
            return "reinforcement removed";
        }

        if (action.Equals("UnlockAndRemoveReinforcement", StringComparison.OrdinalIgnoreCase))
        {
            reinforcement.ClearReinforcement(pos);
            return AppendMarketResetResult(record, "lock and reinforcement removed");
        }

        return $"unknown expiration action '{action}'";
    }

    private string AppendMarketResetResult(RentalRecord record, string result)
    {
        string marketResetResult = marketStallResetService.ResetAssociatedStall(record);
        return string.IsNullOrWhiteSpace(marketResetResult)
            ? result
            : $"{result}; {marketResetResult}";
    }

    private static string TryUnlockOnly(ModSystemBlockReinforcement reinforcement, BlockPos pos)
    {
        try
        {
            MethodInfo? getMethod = AccessTools.Method(typeof(ModSystemBlockReinforcement), "getOrCreateReinforcmentsAt", new[] { typeof(BlockPos) });
            MethodInfo? indexMethod = AccessTools.Method(typeof(ModSystemBlockReinforcement), "toLocalIndex", new[] { typeof(BlockPos) });
            MethodInfo? saveMethod = AccessTools.Method(typeof(ModSystemBlockReinforcement), "SaveReinforcments", new[] { typeof(Dictionary<int, BlockReinforcement>), typeof(BlockPos) });
            if (getMethod == null || indexMethod == null || saveMethod == null)
            {
                return "unable to access reinforcement internals";
            }

            object? dictObject = getMethod.Invoke(reinforcement, new object[] { pos });
            if (dictObject is not Dictionary<int, BlockReinforcement> reinforcements)
            {
                return "reinforcement dictionary unavailable";
            }

            int localIndex = (int)indexMethod.Invoke(reinforcement, new object[] { pos })!;
            if (!reinforcements.TryGetValue(localIndex, out BlockReinforcement? blockReinforcement) || blockReinforcement == null)
            {
                return "no reinforcement found";
            }

            blockReinforcement.Locked = false;
            blockReinforcement.LockedByItemCode = string.Empty;
            saveMethod.Invoke(reinforcement, new object[] { reinforcements, pos });
            return "lock removed";
        }
        catch (Exception exception)
        {
            return $"unlock failed: {exception.GetType().Name}";
        }
    }
}
