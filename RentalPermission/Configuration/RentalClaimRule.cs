namespace RentalPermission;

public class RentalClaimRule
{
    public string Name { get; set; } = "Starter city rentals";

    public bool Enabled { get; set; } = true;

    public int[] AllowedClaimIds { get; set; } = Array.Empty<int>();

    public string[] AllowedClaimOwnerNames { get; set; } = Array.Empty<string>();

    public string[] AllowedClaimDescriptions { get; set; } = Array.Empty<string>();

    public RentalRule[] Rules { get; set; } =
    {
        new RentalRule()
    };
}
