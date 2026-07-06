namespace RentalPermission;

public class RentalPermissionConfig
{
    public string CurrencyItemCode { get; set; } = "game:gear-rusty";

    public double DaysPerMonth { get; set; } = 30;

    public double MonthsPerYear { get; set; } = 12;

    public bool LogRentalEvents { get; set; } = true;

    public bool LogIgnoredInteractions { get; set; } = false;

    public bool EnableExpirationProcessing { get; set; } = true;

    public int ExpirationCheckIntervalSeconds { get; set; } = 300;

    public int MaxExpirationsPerCheck { get; set; } = 20;

    public double DeleteProcessedRentalsAfterHours { get; set; } = 24;

    public RentalClaimRule[] Claims { get; set; } = Array.Empty<RentalClaimRule>();

    public static RentalPermissionConfig CreateDefault()
    {
        return new RentalPermissionConfig
        {
            Claims = new[]
            {
                new RentalClaimRule()
            }
        };
    }
}
