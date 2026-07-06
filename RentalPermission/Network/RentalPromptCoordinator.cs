using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RentalPermission;

internal delegate bool RentalRenewalHandler(IServerPlayer player, BlockPos pos, RentalActionType actionType, double requestedDurationHours, string description);

internal delegate bool RentalItemActionHandler(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, ref EnumHandHandling handling, double requestedDurationHours, string description);

internal sealed class RentalPromptCoordinator
{
    private readonly System.Func<ICoreServerAPI?> getServerApi;
    private readonly RentalPromptService promptService;
    private readonly RentalDurationCalculator durationCalculator;
    private readonly RentalTextFormatter textFormatter;
    private readonly System.Action<IServerPlayer, string, bool> notifyPlayer;
    private readonly System.Action<IServerPlayer, BlockPos> handleConfirmedProtectionRemoval;
    private readonly RentalRenewalHandler handleRenewal;
    private readonly RentalItemActionHandler handleReinforcement;
    private readonly RentalItemActionHandler handleLock;
    private readonly System.Func<IServerPlayer, Block, BlockPos?, string> getBlockDisplayName;
    private readonly System.Func<IServerPlayer, string> getCurrencyDisplayName;

    public RentalPromptCoordinator(
        System.Func<ICoreServerAPI?> getServerApi,
        RentalPromptService promptService,
        RentalDurationCalculator durationCalculator,
        RentalTextFormatter textFormatter,
        System.Action<IServerPlayer, string, bool> notifyPlayer,
        System.Action<IServerPlayer, BlockPos> handleConfirmedProtectionRemoval,
        RentalRenewalHandler handleRenewal,
        RentalItemActionHandler handleReinforcement,
        RentalItemActionHandler handleLock,
        System.Func<IServerPlayer, Block, BlockPos?, string> getBlockDisplayName,
        System.Func<IServerPlayer, string> getCurrencyDisplayName)
    {
        this.getServerApi = getServerApi;
        this.promptService = promptService;
        this.durationCalculator = durationCalculator;
        this.textFormatter = textFormatter;
        this.notifyPlayer = notifyPlayer;
        this.handleConfirmedProtectionRemoval = handleConfirmedProtectionRemoval;
        this.handleRenewal = handleRenewal;
        this.handleReinforcement = handleReinforcement;
        this.handleLock = handleLock;
        this.getBlockDisplayName = getBlockDisplayName;
        this.getCurrencyDisplayName = getCurrencyDisplayName;
    }

    public void Register(ICoreServerAPI api)
    {
        promptService.Register(api, OnRentalConfirm, OnRentalCancel);
    }

    public void SendRentalPrompt(IServerPlayer player, BlockPos pos, RentalActionType actionType, Block block, RentalRule rule, int price, int available, RentalRecord? renewalRecord = null)
    {
        string requestId = RentalRecordTools.CreateRentalId();
        PendingRentalPrompt prompt = new PendingRentalPrompt
        {
            RequestId = requestId,
            PlayerUID = player.PlayerUID,
            Position = pos.Copy(),
            ActionType = actionType,
            CreatedAtUtc = DateTime.UtcNow,
            IsRenewal = renewalRecord != null
        };

        double maxDuration = durationCalculator.GetMaxDurationHours(rule);
        double minDuration = durationCalculator.NormalizeMinDuration(rule);
        double step = durationCalculator.NormalizeDurationStep(rule, minDuration, maxDuration);
        double selectedDuration = maxDuration;
        string durationUnit = RentalDurationCalculator.NormalizeDurationUnit(rule.RentDurationUnit);

        RentalPromptPacket packet = new RentalPromptPacket
        {
            RequestId = requestId,
            ActionType = actionType.ToString(),
            Dimension = pos.dimension,
            X = pos.X,
            Y = pos.Y,
            Z = pos.Z,
            BlockName = getBlockDisplayName(player, block, pos),
            RuleName = rule.Name,
            CurrencyName = getCurrencyDisplayName(player),
            Price = price,
            Available = available,
            Duration = textFormatter.FormatDuration(player, selectedDuration),
            MinDurationHours = minDuration,
            MaxDurationHours = maxDuration,
            DurationStepHours = step,
            SelectedDurationHours = selectedDuration,
            DurationUnit = durationUnit,
            MinDurationValue = RentalDurationCalculator.NormalizeMinDurationValue(rule),
            MaxDurationValue = Math.Max(0, rule.RentDuration),
            DurationStepValue = Math.Max(0, rule.RentDurationStep),
            IsRenewal = renewalRecord != null,
            ExistingDescription = renewalRecord?.Description ?? string.Empty
        };

        if (!promptService.SendPrompt(player, prompt, packet))
        {
            notifyPlayer(player, textFormatter.T(player, "rentalpermission-client-required"), true);
        }
    }

    public void SendProtectionRemovalPrompt(IServerPlayer player, BlockPos pos, Block block, int activeRentalCount)
    {
        string requestId = RentalRecordTools.CreateRentalId();
        PendingRentalPrompt prompt = new PendingRentalPrompt
        {
            RequestId = requestId,
            PlayerUID = player.PlayerUID,
            Position = pos.Copy(),
            ActionType = RentalActionType.RemoveProtection,
            CreatedAtUtc = DateTime.UtcNow,
            PromptKind = "RemoveProtection"
        };

        RentalPromptPacket packet = new RentalPromptPacket
        {
            RequestId = requestId,
            ActionType = RentalActionType.RemoveProtection.ToString(),
            Dimension = pos.dimension,
            X = pos.X,
            Y = pos.Y,
            Z = pos.Z,
            BlockName = getBlockDisplayName(player, block, pos),
            RuleName = activeRentalCount.ToString(),
            PromptKind = "RemoveProtection"
        };

        if (!promptService.SendPrompt(player, prompt, packet))
        {
            notifyPlayer(player, textFormatter.T(player, "rentalpermission-client-required"), true);
        }
    }

    private void OnRentalCancel(IServerPlayer player, RentalCancelPacket packet)
    {
        promptService.CancelPrompt(player, packet.RequestId);
    }

    private void OnRentalConfirm(IServerPlayer player, RentalConfirmPacket packet)
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null)
        {
            return;
        }

        if (!promptService.TryConsumePrompt(player, packet.RequestId, out PendingRentalPrompt? prompt) || prompt == null)
        {
            notifyPlayer(player, textFormatter.T(player, "rentalpermission-request-expired"), true);
            return;
        }

        Block block = sapi.World.BlockAccessor.GetBlock(prompt.Position);
        BlockSelection blockSel = new BlockSelection(prompt.Position.Copy(), BlockFacing.UP, block);
        EnumHandHandling handling = EnumHandHandling.NotHandled;
        ItemSlot activeSlot = player.InventoryManager.ActiveHotbarSlot;

        if (prompt.ActionType == RentalActionType.RemoveProtection || prompt.PromptKind.Equals("RemoveProtection", StringComparison.OrdinalIgnoreCase))
        {
            handleConfirmedProtectionRemoval(player, prompt.Position);
            return;
        }

        string description = RentalRecordTools.NormalizeDescription(packet.Description);
        if (string.IsNullOrWhiteSpace(description))
        {
            notifyPlayer(player, textFormatter.T(player, "rentalpermission-description-required"), true);
            return;
        }

        if (prompt.IsRenewal)
        {
            handleRenewal(player, prompt.Position, prompt.ActionType, packet.SelectedDurationHours, description);
            return;
        }

        if (prompt.ActionType == RentalActionType.Reinforce)
        {
            if (activeSlot.Itemstack?.Collectible is not ItemPlumbAndSquare)
            {
                notifyPlayer(player, textFormatter.T(player, "rentalpermission-hold-reinforcement-tool"), true);
                return;
            }

            handleReinforcement(activeSlot, player.Entity, blockSel, ref handling, packet.SelectedDurationHours, description);
            return;
        }

        if (prompt.ActionType == RentalActionType.Lock)
        {
            if (activeSlot.Itemstack?.Collectible is not ItemPadlock)
            {
                notifyPlayer(player, textFormatter.T(player, "rentalpermission-hold-padlock"), true);
                return;
            }

            handleLock(activeSlot, player.Entity, blockSel, ref handling, packet.SelectedDurationHours, description);
        }
    }
}
