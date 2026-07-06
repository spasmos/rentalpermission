using Vintagestory.API.MathTools;

namespace RentalPermission;

public class PendingRentalPrompt
{
    public string RequestId { get; set; } = string.Empty;
    public string PlayerUID { get; set; } = string.Empty;
    public BlockPos Position { get; set; } = null!;
    public RentalActionType ActionType { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsRenewal { get; set; }
    public string PromptKind { get; set; } = "Rental";
}
