using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class RentalLedger
{
    private readonly Func<ICoreServerAPI?> getServerApi;
    private readonly Func<RentalPermissionConfig> getConfig;
    private readonly Func<RentalPermissionData> getData;
    private readonly Action saveData;
    private readonly System.Func<IServerPlayer, Block, BlockPos, string> getBlockDisplayName;
    private readonly MarketStallResetService marketStallResetService;

    public RentalLedger(
        Func<ICoreServerAPI?> getServerApi,
        Func<RentalPermissionConfig> getConfig,
        Func<RentalPermissionData> getData,
        Action saveData,
        System.Func<IServerPlayer, Block, BlockPos, string> getBlockDisplayName,
        MarketStallResetService marketStallResetService)
    {
        this.getServerApi = getServerApi;
        this.getConfig = getConfig;
        this.getData = getData;
        this.saveData = saveData;
        this.getBlockDisplayName = getBlockDisplayName;
        this.marketStallResetService = marketStallResetService;
    }

    public bool TryGetActiveRentalForPosition(IServerPlayer player, BlockPos pos, out RentalRecord rental)
    {
        rental = GetActiveRentalsForPosition(pos)
            .Where(record => record.PlayerUID == player.PlayerUID)
            .OrderByDescending(record => record.ExpiresAtTotalSeconds)
            .FirstOrDefault()!;
        return rental != null;
    }

    public List<RentalRecord> GetActiveRentalsForPosition(BlockPos pos)
    {
        string posKey = PosKey(pos);
        long now = CurrentElapsedSeconds();
        return (getData().Rentals ?? new List<RentalRecord>())
            .Where(record => record.PositionKey == posKey
                && record.IsActive()
                && (record.ExpiresAtTotalSeconds <= 0 || record.ExpiresAtTotalSeconds > now))
            .ToList();
    }

    public int CountActiveRentals(string playerUid, string claimRuleName, string ruleName)
    {
        long now = CurrentElapsedSeconds();
        return getData().Rentals.Count(record => record.PlayerUID == playerUid
            && record.ClaimRuleName == claimRuleName
            && record.RuleName == ruleName
            && record.IsActive()
            && (record.ExpiresAtTotalSeconds <= 0 || record.ExpiresAtTotalSeconds > now));
    }

    public void RegisterRental(IServerPlayer player, BlockPos pos, RentalClaimRule claimRule, RentalRule rule, Block block, RentalActionType actionType, int paid, double durationHours, string description, MarketStallResetPlan? marketResetPlan = null)
    {
        ICoreServerAPI? sapi = getServerApi();
        long now = CurrentElapsedSeconds();
        long expires = durationHours <= 0 ? 0 : now + (long)(durationHours * 3600);
        string posKey = PosKey(pos);
        RentalPermissionConfig config = getConfig();
        RentalPermissionData data = getData();
        marketResetPlan ??= marketStallResetService.CaptureResetPlan(pos, rule);
        data.Rentals.RemoveAll(record => record.PositionKey == posKey && record.ActionType == actionType.ToString());
        data.Rentals.Add(new RentalRecord
        {
            Id = RentalRecordTools.CreateRentalId(),
            PositionKey = posKey,
            Dimension = pos.dimension,
            X = pos.X,
            Y = pos.Y,
            Z = pos.Z,
            PlayerUID = player.PlayerUID,
            PlayerName = player.PlayerName,
            ClaimRuleName = claimRule.Name,
            RuleName = rule.Name,
            Description = RentalRecordTools.NormalizeDescription(description),
            BlockCode = block.Code?.ToString() ?? string.Empty,
            BlockName = getBlockDisplayName(player, block, pos),
            ActionType = actionType.ToString(),
            PaidAmount = paid,
            CurrencyItemCode = config.CurrencyItemCode,
            CreatedAtTotalSeconds = now,
            ExpiresAtTotalSeconds = expires,
            OnExpired = rule.OnExpired,
            MarketResetEnabled = rule.MarketResetEnabled,
            MarketResetTargetResolved = marketResetPlan.TargetResolved,
            MarketResetResolution = marketResetPlan.Resolution,
            MarketStallDimension = marketResetPlan.Position?.dimension ?? 0,
            MarketStallX = marketResetPlan.Position?.X ?? 0,
            MarketStallY = marketResetPlan.Position?.Y ?? 0,
            MarketStallZ = marketResetPlan.Position?.Z ?? 0,
            MarketStallBlockCode = marketResetPlan.BlockCode,
            Status = RentalStatus.Active
        });
        saveData();

        if (config.LogRentalEvents && sapi != null)
        {
            sapi.Logger.Notification(
                "[RentalPermission] {0} rented {1} at {2} using rule '{3}' for {4} {5}. Expires: {6}.",
                player.PlayerName,
                block.Code,
                posKey,
                rule.Name,
                paid,
                config.CurrencyItemCode,
                expires <= 0 ? "never" : expires.ToString());
        }
    }

    public int CancelRentalsForManualProtectionRemoval(IServerPlayer player, BlockPos pos)
    {
        ICoreServerAPI? sapi = getServerApi();
        long now = CurrentElapsedSeconds();
        string posKey = PosKey(pos);
        List<RentalRecord> activeRentals = GetActiveRentalsForPosition(pos);
        if (activeRentals.Count == 0)
        {
            return 0;
        }

        foreach (RentalRecord record in activeRentals)
        {
            record.Status = RentalStatus.Cancelled;
            record.ProcessedAtTotalSeconds = now;
            record.ExpirationResult = $"manual protection removed by {player.PlayerName}; no refund";
        }

        saveData();

        RentalPermissionConfig config = getConfig();
        if (config.LogRentalEvents && sapi != null)
        {
            sapi.Logger.Notification(
                "[RentalPermission] {0} manually removed protection at {1}; cancelled {2} active rental(s) without refund.",
                player.PlayerName,
                posKey,
                activeRentals.Count);
        }

        return activeRentals.Count;
    }

    public RentalRecord? FindRental(string id)
    {
        return (getData().Rentals ?? new List<RentalRecord>())
            .FirstOrDefault(record => record.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public RentalRule? FindConfiguredRule(RentalRecord record)
    {
        return getConfig().Claims
            .FirstOrDefault(claimRule => claimRule.Name.Equals(record.ClaimRuleName, StringComparison.OrdinalIgnoreCase))
            ?.Rules.FirstOrDefault(rule => rule.Name.Equals(record.RuleName, StringComparison.OrdinalIgnoreCase));
    }

    private long CurrentElapsedSeconds()
    {
        return getServerApi()?.World.Calendar.ElapsedSeconds == null ? 0 : (long)getServerApi()!.World.Calendar.ElapsedSeconds;
    }

    private static string PosKey(BlockPos pos)
    {
        return $"{pos.dimension}/{pos.X}/{pos.Y}/{pos.Z}";
    }

}
