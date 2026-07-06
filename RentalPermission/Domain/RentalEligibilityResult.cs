namespace RentalPermission;

internal sealed class RentalEligibilityResult
{
    private RentalEligibilityResult(bool allowed, int price, string denial, MarketStallResetPlan? marketResetPlan)
    {
        Allowed = allowed;
        Price = price;
        Denial = denial;
        MarketResetPlan = marketResetPlan;
    }

    public bool Allowed { get; }

    public int Price { get; }

    public string Denial { get; }

    public MarketStallResetPlan? MarketResetPlan { get; }

    public static RentalEligibilityResult Allow(int price, MarketStallResetPlan? marketResetPlan = null)
    {
        return new RentalEligibilityResult(true, price, string.Empty, marketResetPlan);
    }

    public static RentalEligibilityResult Deny(int price, string denial, MarketStallResetPlan? marketResetPlan = null)
    {
        return new RentalEligibilityResult(false, price, denial, marketResetPlan);
    }
}
