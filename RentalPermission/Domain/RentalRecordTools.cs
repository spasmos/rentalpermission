namespace RentalPermission;

internal static class RentalRecordTools
{
    private const int MaxDescriptionLength = 120;

    public static string NormalizeDescription(string description)
    {
        string normalized = (description ?? string.Empty).Trim();
        return normalized.Length <= MaxDescriptionLength ? normalized : normalized[..MaxDescriptionLength];
    }

    public static string CreateRentalId()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }
}
