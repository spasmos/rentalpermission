using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RentalPermission;

public class RentalPermissionModSystem : ModSystem
{
    private Harmony? harmony;
    private ICoreServerAPI? sapi;
    private RentalPermissionConfig config = new();
    private RentalPermissionData data = new();
    private long expirationListenerId;
    private readonly SystemRentalState systemRentalState = new();
    private readonly RentalPositionResolver positionResolver;
    private readonly RentalRuleResolver ruleResolver;
    private readonly RentalPaymentService paymentService;
    private readonly MarketStallResetService marketStallResetService;
    private readonly RentalExpirationActionExecutor expirationActionExecutor;
    private readonly RentalExpirationProcessor expirationProcessor;
    private readonly RentalPermissionStore store;
    private readonly RentalDurationCalculator durationCalculator;
    private readonly RentalPromptService promptService = new();
    private readonly RentalTextFormatter textFormatter;
    private readonly RentalLedger ledger;
    private readonly RentalPricingService pricingService;
    private readonly RentalEligibilityService eligibilityService;
    private readonly RentalPermissionCommandService commandService;
    private readonly RentalPromptCoordinator promptCoordinator;
    private readonly RentalInteractionService interactionService;

    public RentalPermissionModSystem()
    {
        positionResolver = new RentalPositionResolver(() => sapi);
        ruleResolver = new RentalRuleResolver(() => sapi, () => config, positionResolver);
        textFormatter = new RentalTextFormatter(() => sapi);
        CurrencyWallet currencyWallet = new CurrencyWallet(() => sapi, () => config.CurrencyItemCode);
        paymentService = new RentalPaymentService(() => config, textFormatter, currencyWallet);
        durationCalculator = new RentalDurationCalculator(() => config);
        marketStallResetService = new MarketStallResetService(() => sapi);
        ledger = new RentalLedger(
            () => sapi,
            () => config,
            () => data,
            SaveData,
            GetBlockDisplayName,
            marketStallResetService);
        pricingService = new RentalPricingService(durationCalculator, ledger);
        eligibilityService = new RentalEligibilityService(() => sapi, ledger, pricingService, textFormatter, marketStallResetService);
        commandService = new RentalPermissionCommandService(
            () => sapi,
            () => config,
            () => data,
            LoadAll,
            SaveData,
            ProcessExpiredRentals,
            ledger,
            ruleResolver,
            textFormatter);
        interactionService = new RentalInteractionService(
            () => sapi,
            () => config,
            SaveData,
            systemRentalState,
            positionResolver,
            ruleResolver,
            ledger,
            eligibilityService,
            pricingService,
            paymentService,
            durationCalculator,
            textFormatter,
            SendRentalPrompt,
            SendProtectionRemovalPrompt,
            NotifyPlayer,
            (player, block, pos) => GetBlockDisplayName(player, block, pos));
        promptCoordinator = new RentalPromptCoordinator(
            () => sapi,
            promptService,
            durationCalculator,
            textFormatter,
            (player, message, error) => NotifyPlayer(player, message, error),
            interactionService.TryHandleConfirmedProtectionRemoval,
            interactionService.TryHandleRenewal,
            interactionService.TryHandleReinforcement,
            interactionService.TryHandleLock,
            (player, block, pos) => GetBlockDisplayName(player, block, pos),
            paymentService.GetCurrencyDisplayName);
        expirationActionExecutor = new RentalExpirationActionExecutor(
            () => sapi,
            marketStallResetService,
            record => GetRentalBlockDisplayName(record),
            (record, langKey, args) => NotifyRentalOwner(record, langKey, args));
        expirationProcessor = new RentalExpirationProcessor(
            () => sapi,
            () => config,
            () => data,
            expirationActionExecutor,
            SaveData,
            NotifyRenewalApplied);
        store = new RentalPermissionStore(() => sapi);
    }

    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Server;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        LoadAll();
        RegisterPrivileges(api);
        RegisterCommands(api);
        RegisterNetwork(api);
        harmony = RentalHarmonyPatcher.Patch(Mod.Info.ModID);
        RegisterExpirationProcessor(api);
        api.Logger.Notification("[RentalPermission] Registered rental handlers.");
    }

    public override void Dispose()
    {
        if (sapi != null && expirationListenerId != 0)
        {
            sapi.Event.UnregisterGameTickListener(expirationListenerId);
            expirationListenerId = 0;
        }

        harmony?.UnpatchAll(Mod.Info.ModID);
        harmony = null;
        sapi = null;
    }

    public static bool OnStrengthenBlockPrefix(
        BlockPos pos,
        IPlayer byPlayer,
        int strength,
        int forGroupUid,
        ref bool __result)
    {
        if (byPlayer is not IServerPlayer player
            || player.Entity.Api.Side != EnumAppSide.Server
            || player.Entity.Api.ModLoader.GetModSystem<RentalPermissionModSystem>() is not { } modSystem)
        {
            return true;
        }

        return modSystem.interactionService.TryStartSystemRental(player, pos, RentalActionType.Reinforce, ref __result);
    }

    public static void OnStrengthenBlockPostfix(
        BlockPos pos,
        IPlayer byPlayer,
        int strength,
        int forGroupUid,
        bool __result)
    {
        if (byPlayer is IServerPlayer player
            && player.Entity.Api.Side == EnumAppSide.Server
            && player.Entity.Api.ModLoader.GetModSystem<RentalPermissionModSystem>() is { } modSystem)
        {
            modSystem.interactionService.FinishSystemRental(player, pos, RentalActionType.Reinforce, __result);
        }
    }

    public static bool OnTryLockPrefix(
        BlockPos pos,
        IPlayer byPlayer,
        string itemCode,
        ref bool __result)
    {
        if (byPlayer is not IServerPlayer player
            || player.Entity.Api.Side != EnumAppSide.Server
            || player.Entity.Api.ModLoader.GetModSystem<RentalPermissionModSystem>() is not { } modSystem)
        {
            return true;
        }

        return modSystem.interactionService.TryStartSystemRental(player, pos, RentalActionType.Lock, ref __result);
    }

    public static void OnTryLockPostfix(
        BlockPos pos,
        IPlayer byPlayer,
        string itemCode,
        bool __result)
    {
        if (byPlayer is IServerPlayer player
            && player.Entity.Api.Side == EnumAppSide.Server
            && player.Entity.Api.ModLoader.GetModSystem<RentalPermissionModSystem>() is { } modSystem)
        {
            modSystem.interactionService.FinishSystemRental(player, pos, RentalActionType.Lock, __result);
        }
    }

    public static void OnTryRemoveReinforcementPostfix(
        BlockPos pos,
        IPlayer forPlayer,
        ref string errorCode,
        bool __result)
    {
        if (!__result
            || forPlayer is not IServerPlayer player
            || player.Entity.Api.Side != EnumAppSide.Server
            || player.Entity.Api.ModLoader.GetModSystem<RentalPermissionModSystem>() is not { } modSystem)
        {
            return;
        }

        modSystem.interactionService.CancelRentalsForManualProtectionRemoval(player, pos);
    }

    public static bool OnPlumbAndSquareAttackPrefix(
        ItemPlumbAndSquare __instance,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        ref EnumHandHandling handling)
    {
        if (byEntity.World.Side != EnumAppSide.Server || blockSel == null)
        {
            return true;
        }

        if (byEntity is not EntityPlayer entityPlayer
            || entityPlayer.Player is not IServerPlayer player
            || player.Entity.Api.ModLoader.GetModSystem<RentalPermissionModSystem>() is not { } modSystem)
        {
            return true;
        }

        return modSystem.interactionService.TryPreflightProtectionRemoval(player, blockSel.Position, ref handling);
    }

    public static bool OnPlumbAndSquareInteractPrefix(
        ItemPlumbAndSquare __instance,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        if (byEntity.World.Side != EnumAppSide.Server || blockSel == null)
        {
            return true;
        }

        if (byEntity.Api.ModLoader.GetModSystem<RentalPermissionModSystem>() is not { } modSystem)
        {
            return true;
        }

        return modSystem.interactionService.TryPreflightRentalInteraction(byEntity, blockSel, RentalActionType.Reinforce, ref handling);
    }

    public static bool OnPadlockInteractPrefix(
        ItemPadlock __instance,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        if (byEntity.World.Side != EnumAppSide.Server || blockSel == null)
        {
            return true;
        }

        if (byEntity.Api.ModLoader.GetModSystem<RentalPermissionModSystem>() is not { } modSystem)
        {
            return true;
        }

        return modSystem.interactionService.TryPreflightRentalInteraction(byEntity, blockSel, RentalActionType.Lock, ref handling);
    }

    private void RegisterNetwork(ICoreServerAPI api)
    {
        promptCoordinator.Register(api);
    }

    private void SendRentalPrompt(IServerPlayer player, BlockPos pos, RentalActionType actionType, Block block, RentalRule rule, int price, int available, RentalRecord? renewalRecord = null)
    {
        promptCoordinator.SendRentalPrompt(player, pos, actionType, block, rule, price, available, renewalRecord);
    }

    private void SendProtectionRemovalPrompt(IServerPlayer player, BlockPos pos, Block block, int activeRentalCount)
    {
        promptCoordinator.SendProtectionRemovalPrompt(player, pos, block, activeRentalCount);
    }

    private void NotifyPlayer(IServerPlayer player, string message, bool error = false)
    {
        EnumChatType chatType = error ? EnumChatType.CommandError : EnumChatType.Notification;
        player.SendMessage(GlobalConstants.GeneralChatGroup, message, chatType);
        player.SendMessage(GlobalConstants.ServerInfoChatGroup, message, chatType);
        if (error)
        {
            player.SendIngameError("rentalpermission", message);
        }
    }

    private string T(IServerPlayer player, string key, params object[] args)
    {
        return textFormatter.T(player, key, args);
    }

    private string GetBlockDisplayName(IServerPlayer player, Block block, BlockPos? pos = null)
    {
        return textFormatter.GetBlockDisplayName(player, block, pos);
    }

    private string GetRentalBlockDisplayName(RentalRecord record, string? langCode = null)
    {
        return textFormatter.GetRentalBlockDisplayName(record, langCode);
    }

    private void RegisterExpirationProcessor(ICoreServerAPI api)
    {
        expirationListenerId = expirationProcessor.Register(api, ProcessExpiredRentals);
    }

    private int ProcessExpiredRentals(string reason)
    {
        return expirationProcessor.Process(reason);
    }

    private void NotifyRentalOwner(RentalRecord record, string langKey, params object[] args)
    {
        if (sapi?.World.PlayerByUid(record.PlayerUID) is IServerPlayer player)
        {
            NotifyPlayer(player, T(player, langKey, args));
        }
    }

    private void NotifyRenewalApplied(RentalRecord record, long now)
    {
        if (sapi?.World.PlayerByUid(record.PlayerUID) is IServerPlayer player)
        {
            NotifyPlayer(player, T(player, "rentalpermission-renewal-applied", record.Description, textFormatter.FormatGameTime(player.LanguageCode, record.ExpiresAtTotalSeconds - now)));
        }
    }

    private void RegisterPrivileges(ICoreServerAPI api)
    {
        RentalPrivilegeRegistrar.Register(api, config);
    }

    private void RegisterCommands(ICoreServerAPI api)
    {
        commandService.Register(api);
    }

    private void LoadAll()
    {
        RentalPermissionSnapshot snapshot = store.Load();
        config = snapshot.Config;
        data = snapshot.Data;
    }

    private void SaveData()
    {
        store.SaveData(data);
    }
}
