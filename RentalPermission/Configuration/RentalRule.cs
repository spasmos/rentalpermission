namespace RentalPermission;

public class RentalRule
{
    public string Name { get; set; } = "Market chests";

    public bool Enabled { get; set; } = true;

    public bool RentOnReinforce { get; set; } = true;

    public bool RentOnLock { get; set; } = true;

    public bool AllowDelegatedRental { get; set; } = true;

    public string RentalPrivilegeCode { get; set; } = "rentblocks";

    public bool RequireUseAccess { get; set; } = true;

    public string[] BlockCodePrefixes { get; set; } =
    {
        "game:chest-",
        "game:labeledchest-",
        "game:trunk-"
    };

    public string[] BlockCodes { get; set; } = Array.Empty<string>();

    public string RentDurationUnit { get; set; } = "hours";

    public double RentDuration { get; set; } = 168;

    public double MinRentDuration { get; set; } = 168;

    public double RentDurationStep { get; set; } = 0;

    public int BasePrice { get; set; } = 10;

    public int RenewalPrice { get; set; } = 10;

    public int IncrementalPricePerExistingRental { get; set; } = 5;

    public int MaxActiveRentalsPerPlayer { get; set; } = 3;

    public string OnExpired { get; set; } = "WarnOnly";

    public bool MarketResetEnabled { get; set; }

    public string[] MarketStallBlockCodePrefixes { get; set; } = Array.Empty<string>();

    public string[] MarketStallBlockCodes { get; set; } = Array.Empty<string>();

    public int MarketStallSearchRadiusBlocks { get; set; } = 5;

    public bool MarketStallRequireUniqueMatch { get; set; } = true;

    public bool AppliesTo(RentalActionType actionType)
    {
        return actionType switch
        {
            RentalActionType.Reinforce => RentOnReinforce,
            RentalActionType.Lock => RentOnLock,
            _ => false
        };
    }
}
