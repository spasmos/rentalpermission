using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class RentalPricingService
{
    private readonly RentalDurationCalculator durationCalculator;
    private readonly RentalLedger ledger;

    public RentalPricingService(RentalDurationCalculator durationCalculator, RentalLedger ledger)
    {
        this.durationCalculator = durationCalculator;
        this.ledger = ledger;
    }

    public int CalculatePrice(IServerPlayer player, RentalClaimRule claimRule, RentalRule rule)
    {
        return CalculatePrice(player, claimRule, rule, durationCalculator.GetMaxDurationHours(rule));
    }

    public int CalculatePrice(IServerPlayer player, RentalClaimRule claimRule, RentalRule rule, double durationHours)
    {
        int existing = ledger.CountActiveRentals(player.PlayerUID, claimRule.Name, rule.Name);
        int fullDurationPrice = Math.Max(0, rule.BasePrice + existing * rule.IncrementalPricePerExistingRental);
        double maxDuration = durationCalculator.GetMaxDurationHours(rule);
        if (fullDurationPrice <= 0 || maxDuration <= 0 || durationHours <= 0)
        {
            return fullDurationPrice;
        }

        return Math.Max(1, (int)Math.Ceiling(fullDurationPrice * durationHours / maxDuration));
    }

    public int CalculateRenewalPrice(RentalRecord activeRental, RentalRule rule, double durationHours)
    {
        int fullDurationPrice = Math.Max(0, rule.RenewalPrice > 0 ? rule.RenewalPrice : activeRental.PaidAmount);
        double maxDuration = durationCalculator.GetMaxDurationHours(rule);
        if (fullDurationPrice <= 0 || maxDuration <= 0 || durationHours <= 0)
        {
            return fullDurationPrice;
        }

        return Math.Max(1, (int)Math.Ceiling(fullDurationPrice * durationHours / maxDuration));
    }

    public int GetRenewalPromptPrice(RentalRecord activeRental, RentalRule rule)
    {
        return Math.Max(0, rule.RenewalPrice > 0 ? rule.RenewalPrice : activeRental.PaidAmount);
    }
}
