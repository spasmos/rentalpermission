using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RentalPermission;

[ProtoContract]
public class RentalPromptPacket
{
    [ProtoMember(1)] public string RequestId { get; set; } = string.Empty;
    [ProtoMember(2)] public string ActionType { get; set; } = string.Empty;
    [ProtoMember(3)] public int Dimension { get; set; }
    [ProtoMember(4)] public int X { get; set; }
    [ProtoMember(5)] public int Y { get; set; }
    [ProtoMember(6)] public int Z { get; set; }
    [ProtoMember(7)] public string BlockName { get; set; } = string.Empty;
    [ProtoMember(8)] public string RuleName { get; set; } = string.Empty;
    [ProtoMember(9)] public string CurrencyName { get; set; } = string.Empty;
    [ProtoMember(10)] public int Price { get; set; }
    [ProtoMember(11)] public int Available { get; set; }
    [ProtoMember(12)] public string Duration { get; set; } = string.Empty;
    [ProtoMember(13)] public double MinDurationHours { get; set; }
    [ProtoMember(14)] public double MaxDurationHours { get; set; }
    [ProtoMember(15)] public double DurationStepHours { get; set; }
    [ProtoMember(16)] public double SelectedDurationHours { get; set; }
    [ProtoMember(17)] public bool IsRenewal { get; set; }
    [ProtoMember(18)] public string ExistingDescription { get; set; } = string.Empty;
    [ProtoMember(19)] public string DurationUnit { get; set; } = "hours";
    [ProtoMember(20)] public double MinDurationValue { get; set; }
    [ProtoMember(21)] public double MaxDurationValue { get; set; }
    [ProtoMember(22)] public double DurationStepValue { get; set; }
    [ProtoMember(23)] public string PromptKind { get; set; } = "Rental";

    public BlockPos Position => new BlockPos(X, Y, Z, Dimension);
}

[ProtoContract]
public class RentalConfirmPacket
{
    [ProtoMember(1)] public string RequestId { get; set; } = string.Empty;
    [ProtoMember(2)] public double SelectedDurationHours { get; set; }
    [ProtoMember(3)] public string Description { get; set; } = string.Empty;
}

[ProtoContract]
public class RentalCancelPacket
{
    [ProtoMember(1)] public string RequestId { get; set; } = string.Empty;
}
