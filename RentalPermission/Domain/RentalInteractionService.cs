using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RentalPermission;

internal delegate void RentalPromptSender(IServerPlayer player, BlockPos pos, RentalActionType actionType, Block block, RentalRule rule, int price, int available, RentalRecord? renewalRecord = null);

internal delegate void ProtectionRemovalPromptSender(IServerPlayer player, BlockPos pos, Block block, int activeRentalCount);

internal sealed class RentalInteractionService
{
    private readonly System.Func<ICoreServerAPI?> getServerApi;
    private readonly Func<RentalPermissionConfig> getConfig;
    private readonly Action saveData;
    private readonly SystemRentalState systemRentalState;
    private readonly RentalPositionResolver positionResolver;
    private readonly RentalRuleResolver ruleResolver;
    private readonly RentalLedger ledger;
    private readonly RentalEligibilityService eligibilityService;
    private readonly RentalPricingService pricingService;
    private readonly RentalPaymentService paymentService;
    private readonly RentalDurationCalculator durationCalculator;
    private readonly RentalTextFormatter textFormatter;
    private readonly RentalPromptSender sendRentalPrompt;
    private readonly ProtectionRemovalPromptSender sendProtectionRemovalPrompt;
    private readonly Action<IServerPlayer, string, bool> notifyPlayer;
    private readonly System.Func<IServerPlayer, Block, BlockPos?, string> getBlockDisplayName;

    public RentalInteractionService(
        System.Func<ICoreServerAPI?> getServerApi,
        Func<RentalPermissionConfig> getConfig,
        Action saveData,
        SystemRentalState systemRentalState,
        RentalPositionResolver positionResolver,
        RentalRuleResolver ruleResolver,
        RentalLedger ledger,
        RentalEligibilityService eligibilityService,
        RentalPricingService pricingService,
        RentalPaymentService paymentService,
        RentalDurationCalculator durationCalculator,
        RentalTextFormatter textFormatter,
        RentalPromptSender sendRentalPrompt,
        ProtectionRemovalPromptSender sendProtectionRemovalPrompt,
        Action<IServerPlayer, string, bool> notifyPlayer,
        System.Func<IServerPlayer, Block, BlockPos?, string> getBlockDisplayName)
    {
        this.getServerApi = getServerApi;
        this.getConfig = getConfig;
        this.saveData = saveData;
        this.systemRentalState = systemRentalState;
        this.positionResolver = positionResolver;
        this.ruleResolver = ruleResolver;
        this.ledger = ledger;
        this.eligibilityService = eligibilityService;
        this.pricingService = pricingService;
        this.paymentService = paymentService;
        this.durationCalculator = durationCalculator;
        this.textFormatter = textFormatter;
        this.sendRentalPrompt = sendRentalPrompt;
        this.sendProtectionRemovalPrompt = sendProtectionRemovalPrompt;
        this.notifyPlayer = notifyPlayer;
        this.getBlockDisplayName = getBlockDisplayName;
    }

    public bool TryStartSystemRental(IServerPlayer player, BlockPos pos, RentalActionType actionType, ref bool result)
    {
        pos = ResolveRentalPosition(pos);
        if (systemRentalState.ConsumeBypass(player, pos, actionType))
        {
            return true;
        }

        if (!TryGetMatchingRule(pos, actionType, out RentalClaimRule? claimRule, out RentalRule? rule, out LandClaim[] claims, out Block? block))
        {
            LogIgnored(player, pos, actionType, block);
            return true;
        }

        if (TryGetActiveRentalForPosition(player, pos, out RentalRecord? activeRental))
        {
            NotifyPlayer(player, "rentalpermission-rental-active", activeRental.Id);
            return true;
        }

        NotifyPlayer(player, "rentalpermission-rental-confirmation-required", true);
        result = false;
        return false;
    }

    public void FinishSystemRental(IServerPlayer player, BlockPos pos, RentalActionType actionType, bool success)
    {
        // The postfix is kept because Harmony patches the vanilla reinforcement system,
        // but rental registration is driven by the explicit confirmation prompt flow.
    }

    public bool TryPreflightRentalInteraction(EntityAgent byEntity, BlockSelection blockSel, RentalActionType actionType, ref EnumHandHandling handling)
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null || byEntity is not EntityPlayer entityPlayer || entityPlayer.Player is not IServerPlayer player)
        {
            return true;
        }

        BlockPos pos = ResolveRentalPosition(blockSel.Position);
        if (!TryGetMatchingRule(pos, actionType, out RentalClaimRule? claimRule, out RentalRule? rule, out LandClaim[] claims, out Block? block))
        {
            LogIgnored(player, pos, actionType, block);
            return true;
        }

        if (TryGetActiveRentalForPosition(player, pos, out RentalRecord? activeRental))
        {
            if (ShouldOpenRenewalPrompt(player, pos, actionType, block, activeRental))
            {
                sendRentalPrompt(player, pos, actionType, block, rule, pricingService.GetRenewalPromptPrice(activeRental, rule), paymentService.CountCurrency(player), activeRental);
                handling = EnumHandHandling.PreventDefault;
                return false;
            }

            systemRentalState.AddBypass(player, pos, actionType);
            NotifyPlayer(player, "rentalpermission-rental-active", activeRental.Id);
            return true;
        }

        RentalEligibilityResult eligibility = eligibilityService.Evaluate(player, pos, claimRule, rule);
        int price = eligibility.Price;
        if (!eligibility.Allowed)
        {
            notifyPlayer(player, eligibility.Denial, true);
            handling = EnumHandHandling.PreventDefault;
            return false;
        }

        if (actionType == RentalActionType.Reinforce)
        {
            if (!block.HasBehavior<BlockBehaviorReinforcable>(false))
            {
                NotifyPlayer(player, "rentalpermission-not-reinforceable", true);
                handling = EnumHandHandling.PreventDefault;
                return false;
            }

            ModSystemBlockReinforcement reinforcementSystem = sapi.ModLoader.GetModSystem<ModSystemBlockReinforcement>(true);
            if (reinforcementSystem.GetReinforcment(pos)?.Strength > 0)
            {
                return true;
            }

            if (reinforcementSystem.FindResourceForReinforcing(player) == null)
            {
                NotifyPlayer(player, "rentalpermission-no-reinforcement-material", true);
                handling = EnumHandHandling.PreventDefault;
                return false;
            }
        }

        if (actionType == RentalActionType.Lock)
        {
            if (!block.HasBehavior<BlockBehaviorLockable>(true))
            {
                return true;
            }

            ModSystemBlockReinforcement reinforcementSystem = sapi.ModLoader.GetModSystem<ModSystemBlockReinforcement>(true);
            BlockReinforcement? reinforcement = reinforcementSystem.GetReinforcment(pos);
            if (reinforcement == null || reinforcement.Strength <= 0)
            {
                systemRentalState.AddBypass(player, pos, actionType);
                return true;
            }

            if (reinforcement.Locked)
            {
                systemRentalState.AddBypass(player, pos, actionType);
                return true;
            }
        }

        sendRentalPrompt(player, pos, actionType, block, rule, price, paymentService.CountCurrency(player));
        handling = EnumHandHandling.PreventDefault;
        return false;
    }

    public bool TryPreflightProtectionRemoval(IServerPlayer player, BlockPos clickedPos, ref EnumHandHandling handling)
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null)
        {
            return true;
        }

        BlockPos pos = ResolveRentalPosition(clickedPos);
        if (systemRentalState.ConsumeBypass(player, pos, RentalActionType.RemoveProtection))
        {
            return true;
        }

        ModSystemBlockReinforcement reinforcementSystem = sapi.ModLoader.GetModSystem<ModSystemBlockReinforcement>(true);
        BlockReinforcement? reinforcement = reinforcementSystem.GetReinforcment(pos);
        if (reinforcement == null)
        {
            return true;
        }

        List<RentalRecord> activeRentals = ledger.GetActiveRentalsForPosition(pos);
        if (activeRentals.Count == 0)
        {
            return true;
        }

        Block block = sapi.World.BlockAccessor.GetBlock(pos);
        sendProtectionRemovalPrompt(player, pos, block, activeRentals.Count);
        handling = EnumHandHandling.PreventDefault;
        return false;
    }

    public bool TryHandleReinforcement(ItemSlot toolSlot, EntityAgent byEntity, BlockSelection blockSel, ref EnumHandHandling handling, double requestedDurationHours = -1, string description = "")
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null || byEntity is not EntityPlayer entityPlayer || entityPlayer.Player is not IServerPlayer player)
        {
            return true;
        }

        BlockPos pos = ResolveRentalPosition(blockSel.Position);
        if (!TryGetMatchingRule(pos, RentalActionType.Reinforce, out RentalClaimRule? claimRule, out RentalRule? rule, out LandClaim[] claims, out Block? block))
        {
            LogIgnored(player, pos, RentalActionType.Reinforce, block);
            return true;
        }

        if (TryGetActiveRentalForPosition(player, pos, out RentalRecord? activeRental))
        {
            NotifyPlayer(player, "rentalpermission-rental-active", activeRental.Id);
            handling = EnumHandHandling.PreventDefault;
            return false;
        }

        RentalEligibilityResult eligibility = eligibilityService.Evaluate(player, pos, claimRule, rule);
        int price = eligibility.Price;
        if (!eligibility.Allowed)
        {
            notifyPlayer(player, eligibility.Denial, true);
            handling = EnumHandHandling.PreventDefault;
            return false;
        }

        double rentalDurationHours = durationCalculator.NormalizeRequestedDuration(rule, requestedDurationHours);
        price = pricingService.CalculatePrice(player, claimRule, rule, rentalDurationHours);

        if (!block.HasBehavior<BlockBehaviorReinforcable>(false))
        {
            NotifyPlayer(player, "rentalpermission-not-reinforceable", true);
            handling = EnumHandHandling.PreventDefault;
            return false;
        }

        ModSystemBlockReinforcement reinforcementSystem = sapi.ModLoader.GetModSystem<ModSystemBlockReinforcement>(true);
        if (reinforcementSystem.GetReinforcment(pos)?.Strength > 0)
        {
            return true;
        }

        ItemSlot reinforcementMaterialSlot = reinforcementSystem.FindResourceForReinforcing(player);
        if (reinforcementMaterialSlot == null)
        {
            handling = EnumHandHandling.PreventDefault;
            return false;
        }

        if (!paymentService.TryCharge(player, price, out string paymentError))
        {
            notifyPlayer(player, paymentError, true);
            handling = EnumHandHandling.PreventDefault;
            return false;
        }

        int strength = reinforcementMaterialSlot.Itemstack?.ItemAttributes?["reinforcementStrength"].AsInt(0) ?? 0;
        int toolMode = toolSlot.Itemstack?.Attributes.GetInt("toolMode", 0) ?? 0;
        int groupUid = GetSelectedGroupUid(player, toolMode);
        systemRentalState.AddBypass(player, pos, RentalActionType.Reinforce);
        bool strengthened;
        try
        {
            strengthened = groupUid > 0
                ? reinforcementSystem.StrengthenBlock(pos, player, strength, groupUid)
                : reinforcementSystem.StrengthenBlock(pos, player, strength);
        }
        finally
        {
            systemRentalState.RemoveBypass(player, pos, RentalActionType.Reinforce);
        }

        if (!strengthened)
        {
            paymentService.Refund(player, price);
            NotifyPlayer(player, "rentalpermission-already-reinforced", true);
            handling = EnumHandHandling.PreventDefault;
            return false;
        }

        reinforcementMaterialSlot.TakeOut(1);
        reinforcementMaterialSlot.MarkDirty();
        sapi.World.PlaySoundAt(new AssetLocation("sounds/tool/reinforce"), pos, 0.0, null, true, 32f, 1f);
        NotifyPlayer(player, "rentalpermission-payment-accepted", price, paymentService.GetCurrencyDisplayName(player), rule.Name, textFormatter.FormatDuration(player, rentalDurationHours));
        RegisterRental(player, pos, claimRule, rule, block, RentalActionType.Reinforce, price, rentalDurationHours, description, eligibility.MarketResetPlan);
        NotifyPlayer(player, "rentalpermission-rental-registered", description, PosKey(pos));
        handling = EnumHandHandling.PreventDefault;
        return false;
    }

    public bool TryHandleLock(ItemSlot padlockSlot, EntityAgent byEntity, BlockSelection blockSel, ref EnumHandHandling handling, double requestedDurationHours = -1, string description = "")
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null || byEntity is not EntityPlayer entityPlayer || entityPlayer.Player is not IServerPlayer player)
        {
            return true;
        }

        BlockPos pos = ResolveRentalPosition(blockSel.Position);
        if (!TryGetMatchingRule(pos, RentalActionType.Lock, out RentalClaimRule? claimRule, out RentalRule? rule, out LandClaim[] claims, out Block? block))
        {
            LogIgnored(player, pos, RentalActionType.Lock, block);
            return true;
        }

        if (TryGetActiveRentalForPosition(player, pos, out RentalRecord? activeRental))
        {
            systemRentalState.AddBypass(player, pos, RentalActionType.Lock);
            NotifyPlayer(player, "rentalpermission-rental-active", activeRental.Id);
            return true;
        }

        RentalEligibilityResult eligibility = eligibilityService.Evaluate(player, pos, claimRule, rule);
        int price = eligibility.Price;
        if (!eligibility.Allowed)
        {
            notifyPlayer(player, eligibility.Denial, true);
            handling = EnumHandHandling.PreventDefault;
            return false;
        }

        double rentalDurationHours = durationCalculator.NormalizeRequestedDuration(rule, requestedDurationHours);
        price = pricingService.CalculatePrice(player, claimRule, rule, rentalDurationHours);

        if (!block.HasBehavior<BlockBehaviorLockable>(true))
        {
            return true;
        }

        ModSystemBlockReinforcement reinforcementSystem = sapi.ModLoader.GetModSystem<ModSystemBlockReinforcement>(true);
        BlockReinforcement? reinforcement = reinforcementSystem.GetReinforcment(pos);
        if (reinforcement == null || reinforcement.Strength <= 0)
        {
            systemRentalState.AddBypass(player, pos, RentalActionType.Lock);
            return true;
        }

        if (reinforcement.Locked)
        {
            systemRentalState.AddBypass(player, pos, RentalActionType.Lock);
            return true;
        }

        if (!paymentService.TryCharge(player, price, out string paymentError))
        {
            notifyPlayer(player, paymentError, true);
            handling = EnumHandHandling.PreventDefault;
            return false;
        }

        string itemCode = padlockSlot.Itemstack?.Collectible?.Code?.ToString() ?? string.Empty;
        systemRentalState.AddBypass(player, pos, RentalActionType.Lock);
        bool locked;
        try
        {
            locked = reinforcementSystem.TryLock(pos, player, itemCode);
        }
        finally
        {
            systemRentalState.RemoveBypass(player, pos, RentalActionType.Lock);
        }

        if (!locked)
        {
            paymentService.Refund(player, price);
            NotifyPlayer(player, "rentalpermission-cannot-lock", true);
            handling = EnumHandHandling.PreventDefault;
            return false;
        }

        sapi.World.PlaySoundAt(new AssetLocation("sounds/tool/padlock.ogg"), player, player, false, 12f, 1f);
        padlockSlot.TakeOut(1);
        padlockSlot.MarkDirty();
        NotifyPlayer(player, "rentalpermission-payment-accepted", price, paymentService.GetCurrencyDisplayName(player), rule.Name, textFormatter.FormatDuration(player, rentalDurationHours));
        RegisterRental(player, pos, claimRule, rule, block, RentalActionType.Lock, price, rentalDurationHours, description, eligibility.MarketResetPlan);
        NotifyPlayer(player, "rentalpermission-rental-registered", description, PosKey(pos));
        handling = EnumHandHandling.PreventDefault;
        return false;
    }

    public bool TryHandleRenewal(IServerPlayer player, BlockPos pos, RentalActionType actionType, double requestedDurationHours, string description)
    {
        pos = ResolveRentalPosition(pos);
        if (!TryGetActiveRentalForPosition(player, pos, out RentalRecord? activeRental))
        {
            NotifyPlayer(player, "rentalpermission-request-expired", true);
            return false;
        }

        if (activeRental.PendingRenewalPaidAmount > 0)
        {
            NotifyPlayer(player, "rentalpermission-renewal-already-pending", true, activeRental.Id);
            return false;
        }

        RentalRule? rule = ledger.FindConfiguredRule(activeRental);
        if (rule == null)
        {
            NotifyPlayer(player, "rentalpermission-rental-not-found", true, activeRental.Id);
            return false;
        }

        double durationHours = durationCalculator.NormalizeRequestedDuration(rule, requestedDurationHours);
        if (durationHours <= 0)
        {
            NotifyPlayer(player, "rentalpermission-renew-non-expiring", true);
            return false;
        }

        int price = pricingService.CalculateRenewalPrice(activeRental, rule, durationHours);
        if (!paymentService.TryCharge(player, price, out string error))
        {
            notifyPlayer(player, error, true);
            return false;
        }

        long now = (long)getServerApi()!.World.Calendar.ElapsedSeconds;
        activeRental.Description = RentalRecordTools.NormalizeDescription(description);
        activeRental.PendingRenewalPaidAmount = price;
        activeRental.PendingRenewalDurationHours = durationHours;
        activeRental.PendingRenewalCreatedAtTotalSeconds = now;
        activeRental.PendingRenewalCurrencyItemCode = getConfig().CurrencyItemCode;
        saveData();

        NotifyPlayer(player, "rentalpermission-renewal-pending", activeRental.Id, price, paymentService.GetCurrencyDisplayName(player), textFormatter.FormatDuration(player, durationHours));
        return true;
    }

    public void TryHandleConfirmedProtectionRemoval(IServerPlayer player, BlockPos pos)
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null)
        {
            return;
        }

        pos = ResolveRentalPosition(pos);
        if (ledger.GetActiveRentalsForPosition(pos).Count == 0)
        {
            NotifyPlayer(player, "rentalpermission-request-expired", true);
            return;
        }

        if (player.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible is not ItemPlumbAndSquare)
        {
            NotifyPlayer(player, "rentalpermission-hold-reinforcement-tool", true);
            return;
        }

        ModSystemBlockReinforcement reinforcement = sapi.ModLoader.GetModSystem<ModSystemBlockReinforcement>(true);
        BlockReinforcement? existingReinforcement = reinforcement.GetReinforcment(pos);
        string lockedByItemCode = existingReinforcement?.Locked == true
            ? existingReinforcement.LockedByItemCode
            : string.Empty;
        string errorCode = string.Empty;
        systemRentalState.AddBypass(player, pos, RentalActionType.RemoveProtection);
        bool removed;
        try
        {
            removed = reinforcement.TryRemoveReinforcement(pos, player, ref errorCode);
        }
        finally
        {
            systemRentalState.RemoveBypass(player, pos, RentalActionType.RemoveProtection);
        }

        if (!removed)
        {
            NotifyPlayer(player, "rentalpermission-remove-protection-failed", true, errorCode);
            return;
        }

        ReturnPadlockToPlayer(player, lockedByItemCode);
    }

    public void CancelRentalsForManualProtectionRemoval(IServerPlayer player, BlockPos pos)
    {
        pos = ResolveRentalPosition(pos);
        int cancelled = ledger.CancelRentalsForManualProtectionRemoval(player, pos);
        if (cancelled == 0)
        {
            return;
        }

        NotifyPlayer(player, "rentalpermission-manual-protection-removed", cancelled);
    }

    private bool ShouldOpenRenewalPrompt(IServerPlayer player, BlockPos pos, RentalActionType actionType, Block block, RentalRecord activeRental)
    {
        if (activeRental.PendingRenewalPaidAmount > 0)
        {
            NotifyPlayer(player, "rentalpermission-renewal-already-pending", activeRental.Id);
            return false;
        }

        ModSystemBlockReinforcement reinforcementSystem = getServerApi()!.ModLoader.GetModSystem<ModSystemBlockReinforcement>(true);
        BlockReinforcement? reinforcement = reinforcementSystem.GetReinforcment(pos);
        if (actionType == RentalActionType.Lock && reinforcement != null && reinforcement.Strength > 0 && !reinforcement.Locked)
        {
            return false;
        }

        return actionType == RentalActionType.Reinforce || (actionType == RentalActionType.Lock && block.HasBehavior<BlockBehaviorLockable>(true));
    }

    private void ReturnPadlockToPlayer(IServerPlayer player, string itemCode)
    {
        if (string.IsNullOrWhiteSpace(itemCode))
        {
            return;
        }

        ICoreServerAPI? sapi = getServerApi();
        Item? item = sapi?.World.GetItem(new AssetLocation(itemCode));
        if (sapi == null || item == null)
        {
            return;
        }

        ItemStack stack = new(item, 1);
        if (!player.InventoryManager.TryGiveItemstack(stack, true))
        {
            sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
        }
    }

    private void LogIgnored(IServerPlayer player, BlockPos pos, RentalActionType actionType, Block? block)
    {
        ICoreServerAPI? sapi = getServerApi();
        RentalPermissionConfig config = getConfig();
        if (sapi == null || !config.LogIgnoredInteractions)
        {
            return;
        }

        string blockCode = block?.Code?.ToString() ?? sapi.World.BlockAccessor.GetBlock(pos).Code?.ToString() ?? "unknown";
        LandClaim[] claims = sapi.World.Claims.Get(pos) ?? Array.Empty<LandClaim>();
        string claimSummary = claims.Length == 0
            ? "no claim"
            : string.Join("; ", claims.Select(claim => $"id={ruleResolver.GetOwnerClaimId(claim)}, owner='{claim.LastKnownOwnerName ?? claim.OwnedByPlayerUid ?? claim.OwnedByPlayerGroupUid.ToString()}', desc='{claim.Description ?? string.Empty}'"));

        sapi.Logger.Notification(
            "[RentalPermission] Ignored {0} by {1} at {2}: block={3}, claims={4}. No configured rental rule matched.",
            actionType,
            player.PlayerName,
            PosKey(pos),
            blockCode,
            claimSummary);
    }

    private bool TryGetMatchingRule(
        BlockPos pos,
        RentalActionType actionType,
        out RentalClaimRule claimRule,
        out RentalRule rule,
        out LandClaim[] claims,
        out Block block)
    {
        claimRule = null!;
        rule = null!;
        claims = Array.Empty<LandClaim>();
        block = null!;

        if (getServerApi() == null || !ruleResolver.TryGetMatchingRule(pos, actionType, out RentalRuleMatch? match) || match == null)
        {
            return false;
        }

        claimRule = match.ClaimRule;
        rule = match.Rule;
        claims = match.Claims;
        block = match.Block;
        return true;
    }

    private BlockPos ResolveRentalPosition(BlockPos pos)
    {
        return positionResolver.Resolve(pos);
    }

    private bool TryGetActiveRentalForPosition(IServerPlayer player, BlockPos pos, out RentalRecord rental)
    {
        return ledger.TryGetActiveRentalForPosition(player, pos, out rental);
    }

    private void RegisterRental(IServerPlayer player, BlockPos pos, RentalClaimRule claimRule, RentalRule rule, Block block, RentalActionType actionType, int paid, double durationHours, string description, MarketStallResetPlan? marketResetPlan = null)
    {
        ledger.RegisterRental(player, pos, claimRule, rule, block, actionType, paid, durationHours, description, marketResetPlan);
    }

    private void NotifyPlayer(IServerPlayer player, string key, params object[] args)
    {
        notifyPlayer(player, textFormatter.T(player, key, args), false);
    }

    private void NotifyPlayer(IServerPlayer player, string key, bool error, params object[] args)
    {
        notifyPlayer(player, textFormatter.T(player, key, args), error);
    }

    private static int GetSelectedGroupUid(IServerPlayer player, int toolMode)
    {
        if (toolMode <= 0)
        {
            return 0;
        }

        PlayerGroupMembership[] groups = player.GetGroups();
        return toolMode - 1 < groups.Length ? groups[toolMode - 1].GroupUid : 0;
    }

    private static string PosKey(BlockPos pos)
    {
        return $"{pos.dimension}/{pos.X}/{pos.Y}/{pos.Z}";
    }
}
