using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class MarketStallResetService
{
    private readonly Func<ICoreServerAPI?> getServerApi;

    public MarketStallResetService(Func<ICoreServerAPI?> getServerApi)
    {
        this.getServerApi = getServerApi;
    }

    public MarketStallResetPlan CaptureResetPlan(BlockPos rentalPos, RentalRule rule)
    {
        if (!rule.MarketResetEnabled)
        {
            return MarketStallResetPlan.Disabled;
        }

        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null)
        {
            return MarketStallResetPlan.Unresolved(
                "server API unavailable while creating rental",
                "rentalpermission-denied-market-api");
        }

        if (rule.MarketStallBlockCodes.Length == 0 && rule.MarketStallBlockCodePrefixes.Length == 0)
        {
            return MarketStallResetPlan.Unresolved(
                "no market stall block filters configured",
                "rentalpermission-denied-market-no-filters");
        }

        List<MarketStallCandidate> candidates = FindCandidates(sapi, rentalPos, rule);
        if (candidates.Count == 0)
        {
            return MarketStallResetPlan.Unresolved(
                $"no matching market stall found within {Math.Max(0, rule.MarketStallSearchRadiusBlocks)} block(s) at rental creation",
                "rentalpermission-denied-market-no-match",
                Math.Max(0, rule.MarketStallSearchRadiusBlocks));
        }

        if (rule.MarketStallRequireUniqueMatch && candidates.Count > 1)
        {
            string positions = string.Join(", ", candidates.Select(candidate => PosKey(candidate.Position)));
            return MarketStallResetPlan.Unresolved(
                $"multiple matching market stalls found at rental creation ({positions})",
                "rentalpermission-denied-market-multiple",
                candidates.Count,
                positions);
        }

        MarketStallCandidate selected = SelectCandidate(candidates);
        string blockCode = selected.Block.Code?.ToString() ?? string.Empty;
        return MarketStallResetPlan.Resolved(selected.Position, blockCode);
    }

    public string ResetAssociatedStall(RentalRecord record)
    {
        if (!record.MarketResetEnabled)
        {
            return string.Empty;
        }

        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null)
        {
            return "market reset skipped: server API unavailable";
        }

        if (!record.MarketResetTargetResolved)
        {
            string reason = string.IsNullOrWhiteSpace(record.MarketResetResolution)
                ? "target was not resolved when rental was created"
                : record.MarketResetResolution;
            return $"market reset skipped: {reason}";
        }

        BlockPos stallPos = new(record.MarketStallX, record.MarketStallY, record.MarketStallZ, record.MarketStallDimension);
        return ResetBlock(sapi, stallPos, record.MarketStallBlockCode);
    }

    private static List<MarketStallCandidate> FindCandidates(ICoreServerAPI sapi, BlockPos rentalPos, RentalRule rule)
    {
        int radius = Math.Max(0, rule.MarketStallSearchRadiusBlocks);
        List<MarketStallCandidate> candidates = new();

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    BlockPos pos = new(rentalPos.X + dx, rentalPos.Y + dy, rentalPos.Z + dz, rentalPos.dimension);
                    Block block = sapi.World.BlockAccessor.GetBlock(pos);
                    string blockCode = block.Code?.ToString() ?? string.Empty;
                    if (!Matches(blockCode, rule))
                    {
                        continue;
                    }

                    candidates.Add(new MarketStallCandidate(pos, block, dx * dx + dy * dy + dz * dz));
                }
            }
        }

        return candidates;
    }

    private static MarketStallCandidate SelectCandidate(IEnumerable<MarketStallCandidate> candidates)
    {
        return candidates
            .OrderBy(candidate => candidate.DistanceSquared)
            .ThenBy(candidate => candidate.Position.X)
            .ThenBy(candidate => candidate.Position.Y)
            .ThenBy(candidate => candidate.Position.Z)
            .First();
    }

    private static bool Matches(string blockCode, RentalRule rule)
    {
        if (string.IsNullOrWhiteSpace(blockCode))
        {
            return false;
        }

        if (rule.MarketStallBlockCodes.Any(code => blockCode.Equals(code, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return rule.MarketStallBlockCodePrefixes.Any(prefix =>
            !string.IsNullOrWhiteSpace(prefix)
            && blockCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResetBlock(ICoreServerAPI sapi, BlockPos pos, string expectedBlockCode)
    {
        IBlockAccessor blockAccessor = sapi.World.BlockAccessor;
        Block currentBlock = blockAccessor.GetBlock(pos);
        string currentBlockCode = currentBlock.Code?.ToString() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(expectedBlockCode)
            && !currentBlockCode.Equals(expectedBlockCode, StringComparison.OrdinalIgnoreCase))
        {
            return $"market reset skipped: expected {expectedBlockCode} at {PosKey(pos)}, found {currentBlockCode}";
        }

        int blockId = currentBlock.BlockId;
        string blockCode = currentBlock.Code?.ToString() ?? blockId.ToString();

        if (blockId <= 0)
        {
            return $"market reset skipped: invalid block id at {PosKey(pos)}";
        }

        blockAccessor.SetBlock(0, pos);
        blockAccessor.SetBlock(blockId, pos);
        blockAccessor.MarkBlockDirty(pos);
        return $"market stall reset at {PosKey(pos)} ({blockCode})";
    }

    private static string PosKey(BlockPos pos)
    {
        return $"{pos.dimension}/{pos.X}/{pos.Y}/{pos.Z}";
    }

    private sealed record MarketStallCandidate(BlockPos Position, Block Block, int DistanceSquared);
}

internal sealed record MarketStallResetPlan(
    bool Enabled,
    bool TargetResolved,
    BlockPos? Position,
    string BlockCode,
    string Resolution,
    string FailureLangKey,
    object[] FailureArgs)
{
    public static MarketStallResetPlan Disabled { get; } = new(false, false, null, string.Empty, string.Empty, string.Empty, Array.Empty<object>());

    public static MarketStallResetPlan Resolved(BlockPos position, string blockCode)
    {
        return new MarketStallResetPlan(
            true,
            true,
            position.Copy(),
            blockCode,
            $"resolved at {position.dimension}/{position.X}/{position.Y}/{position.Z} ({blockCode})",
            string.Empty,
            Array.Empty<object>());
    }

    public static MarketStallResetPlan Unresolved(string resolution, string failureLangKey, params object[] failureArgs)
    {
        return new MarketStallResetPlan(true, false, null, string.Empty, resolution, failureLangKey, failureArgs);
    }
}
