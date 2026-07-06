using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class RentalRuleMatch
{
    public RentalRuleMatch(RentalClaimRule claimRule, RentalRule rule, LandClaim[] claims, Block block)
    {
        ClaimRule = claimRule;
        Rule = rule;
        Claims = claims;
        Block = block;
    }

    public RentalClaimRule ClaimRule { get; }

    public RentalRule Rule { get; }

    public LandClaim[] Claims { get; }

    public Block Block { get; }
}
