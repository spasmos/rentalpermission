using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class RentalRuleResolver
{
    private readonly Func<ICoreServerAPI?> getServerApi;
    private readonly Func<RentalPermissionConfig> getConfig;
    private readonly RentalPositionResolver positionResolver;

    public RentalRuleResolver(
        Func<ICoreServerAPI?> getServerApi,
        Func<RentalPermissionConfig> getConfig,
        RentalPositionResolver positionResolver)
    {
        this.getServerApi = getServerApi;
        this.getConfig = getConfig;
        this.positionResolver = positionResolver;
    }

    public bool TryGetMatchingRule(BlockPos pos, RentalActionType actionType, out RentalRuleMatch? match)
    {
        match = null;
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null)
        {
            return false;
        }

        BlockPos rentalPos = positionResolver.Resolve(pos);
        LandClaim[] claims = sapi.World.Claims.Get(rentalPos) ?? Array.Empty<LandClaim>();
        if (claims.Length == 0)
        {
            return false;
        }

        Block block = sapi.World.BlockAccessor.GetBlock(rentalPos);
        string blockCode = block.Code?.ToString() ?? string.Empty;
        foreach (RentalClaimRule candidateClaimRule in getConfig().Claims)
        {
            if (!candidateClaimRule.Enabled || !ClaimsMatch(candidateClaimRule, claims))
            {
                continue;
            }

            foreach (RentalRule candidateRule in candidateClaimRule.Rules)
            {
                if (!candidateRule.Enabled || !candidateRule.AppliesTo(actionType) || !MatchesAny(blockCode, candidateRule.BlockCodePrefixes, candidateRule.BlockCodes))
                {
                    continue;
                }

                match = new RentalRuleMatch(candidateClaimRule, candidateRule, claims, block);
                return true;
            }
        }

        return false;
    }

    public bool ClaimsMatch(RentalClaimRule rule, LandClaim[] claims)
    {
        bool idFilterEmpty = rule.AllowedClaimIds.Length == 0;
        bool ownerFilterEmpty = rule.AllowedClaimOwnerNames.Length == 0;
        bool descriptionFilterEmpty = rule.AllowedClaimDescriptions.Length == 0;
        if (idFilterEmpty && ownerFilterEmpty && descriptionFilterEmpty)
        {
            return true;
        }

        foreach (LandClaim claim in claims)
        {
            if (!idFilterEmpty)
            {
                int claimId = GetOwnerClaimId(claim);
                if (claimId >= 0 && rule.AllowedClaimIds.Contains(claimId))
                {
                    return true;
                }
            }

            string ownerName = claim.LastKnownOwnerName ?? string.Empty;
            string ownerUid = claim.OwnedByPlayerUid ?? string.Empty;
            string description = claim.Description ?? string.Empty;

            foreach (string allowedOwner in rule.AllowedClaimOwnerNames)
            {
                if (ownerName.Contains(allowedOwner, StringComparison.OrdinalIgnoreCase)
                    || ownerUid.Equals(allowedOwner, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (string allowedDescription in rule.AllowedClaimDescriptions)
            {
                if (description.Contains(allowedDescription, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public int GetOwnerClaimId(LandClaim targetClaim)
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null)
        {
            return -1;
        }

        int index = 0;
        foreach (LandClaim claim in sapi.World.Claims.All)
        {
            if (BelongsToSameClaimOwner(claim, targetClaim))
            {
                if (ReferenceEquals(claim, targetClaim))
                {
                    return index;
                }

                index++;
            }
        }

        return -1;
    }

    private static bool BelongsToSameClaimOwner(LandClaim claim, LandClaim targetClaim)
    {
        return string.Equals(claim.OwnedByPlayerUid ?? string.Empty, targetClaim.OwnedByPlayerUid ?? string.Empty, StringComparison.Ordinal)
            && claim.OwnedByPlayerGroupUid == targetClaim.OwnedByPlayerGroupUid
            && claim.OwnedByEntityId == targetClaim.OwnedByEntityId;
    }

    private static bool MatchesAny(string code, string[] prefixes, string[] exactCodes)
    {
        foreach (string exactCode in exactCodes)
        {
            if (code.Equals(exactCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (string prefix in prefixes)
        {
            if (code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
