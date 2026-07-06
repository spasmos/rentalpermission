using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class RentalEligibilityService
{
    private readonly Func<ICoreServerAPI?> getServerApi;
    private readonly RentalLedger ledger;
    private readonly RentalPricingService pricingService;
    private readonly RentalTextFormatter textFormatter;
    private readonly MarketStallResetService marketStallResetService;

    public RentalEligibilityService(
        Func<ICoreServerAPI?> getServerApi,
        RentalLedger ledger,
        RentalPricingService pricingService,
        RentalTextFormatter textFormatter,
        MarketStallResetService marketStallResetService)
    {
        this.getServerApi = getServerApi;
        this.ledger = ledger;
        this.pricingService = pricingService;
        this.textFormatter = textFormatter;
        this.marketStallResetService = marketStallResetService;
    }

    public RentalEligibilityResult Evaluate(IServerPlayer player, BlockPos pos, RentalClaimRule claimRule, RentalRule rule)
    {
        ICoreServerAPI? sapi = getServerApi();
        int price = pricingService.CalculatePrice(player, claimRule, rule);
        if (sapi == null)
        {
            return RentalEligibilityResult.Deny(price, "server API unavailable");
        }

        bool hasBuild = sapi.World.Claims.TestAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak) == EnumWorldAccessResponse.Granted;
        bool hasDelegated = rule.AllowDelegatedRental && !string.IsNullOrWhiteSpace(rule.RentalPrivilegeCode) && player.HasPrivilege(rule.RentalPrivilegeCode);
        if (!hasBuild && !hasDelegated)
        {
            return RentalEligibilityResult.Deny(price, textFormatter.T(player, "rentalpermission-denied-build", rule.RentalPrivilegeCode));
        }

        if (rule.RequireUseAccess && sapi.World.Claims.TestAccess(player, pos, EnumBlockAccessFlags.Use) != EnumWorldAccessResponse.Granted)
        {
            return RentalEligibilityResult.Deny(price, textFormatter.T(player, "rentalpermission-denied-use"));
        }

        int existing = ledger.CountActiveRentals(player.PlayerUID, claimRule.Name, rule.Name);
        if (rule.MaxActiveRentalsPerPlayer >= 0 && existing >= rule.MaxActiveRentalsPerPlayer)
        {
            return RentalEligibilityResult.Deny(price, textFormatter.T(player, "rentalpermission-denied-limit", existing, rule.Name, rule.MaxActiveRentalsPerPlayer));
        }

        MarketStallResetPlan marketResetPlan = marketStallResetService.CaptureResetPlan(pos, rule);
        if (marketResetPlan.Enabled && !marketResetPlan.TargetResolved)
        {
            string langKey = string.IsNullOrWhiteSpace(marketResetPlan.FailureLangKey)
                ? "rentalpermission-denied-market-generic"
                : marketResetPlan.FailureLangKey;
            object[] args = marketResetPlan.FailureArgs.Length == 0
                ? new object[] { marketResetPlan.Resolution }
                : marketResetPlan.FailureArgs;

            return RentalEligibilityResult.Deny(price, textFormatter.T(player, langKey, args), marketResetPlan);
        }

        return RentalEligibilityResult.Allow(price, marketResetPlan);
    }
}
