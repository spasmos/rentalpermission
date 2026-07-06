namespace RentalPermission;

internal sealed class RentalPermissionSnapshot
{
    public RentalPermissionSnapshot(RentalPermissionConfig config, RentalPermissionData data)
    {
        Config = config;
        Data = data;
    }

    public RentalPermissionConfig Config { get; }

    public RentalPermissionData Data { get; }
}
