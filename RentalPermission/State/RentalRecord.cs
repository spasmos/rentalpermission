namespace RentalPermission;

public class RentalRecord
{
    public string Id { get; set; } = string.Empty;

    public string Status { get; set; } = RentalStatus.Active;

    public string PositionKey { get; set; } = string.Empty;

    public int Dimension { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Z { get; set; }

    public string PlayerUID { get; set; } = string.Empty;

    public string PlayerName { get; set; } = string.Empty;

    public string ClaimRuleName { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string BlockCode { get; set; } = string.Empty;

    public string BlockName { get; set; } = string.Empty;

    public string ActionType { get; set; } = string.Empty;

    public int PaidAmount { get; set; }

    public string CurrencyItemCode { get; set; } = string.Empty;

    public long CreatedAtTotalSeconds { get; set; }

    public long ExpiresAtTotalSeconds { get; set; }

    public string OnExpired { get; set; } = "WarnOnly";

    public bool MarketResetEnabled { get; set; }

    public bool MarketResetTargetResolved { get; set; }

    public string MarketResetResolution { get; set; } = string.Empty;

    public int MarketStallDimension { get; set; }

    public int MarketStallX { get; set; }

    public int MarketStallY { get; set; }

    public int MarketStallZ { get; set; }

    public string MarketStallBlockCode { get; set; } = string.Empty;

    public long ProcessedAtTotalSeconds { get; set; }

    public string ExpirationResult { get; set; } = string.Empty;

    public int RenewalCount { get; set; }

    public long LastRenewedAtTotalSeconds { get; set; }

    public int PendingRenewalPaidAmount { get; set; }

    public double PendingRenewalDurationHours { get; set; }

    public long PendingRenewalCreatedAtTotalSeconds { get; set; }

    public string PendingRenewalCurrencyItemCode { get; set; } = string.Empty;

    public bool IsActive()
    {
        return StatusOrActive().Equals(RentalStatus.Active, StringComparison.OrdinalIgnoreCase);
    }

    public string StatusOrActive()
    {
        return string.IsNullOrWhiteSpace(Status) ? RentalStatus.Active : Status;
    }
}
