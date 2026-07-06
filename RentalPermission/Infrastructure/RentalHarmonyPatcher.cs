using HarmonyLib;
using Vintagestory.GameContent;

namespace RentalPermission;

internal static class RentalHarmonyPatcher
{
    public static Harmony Patch(string modId)
    {
        Harmony harmony = new Harmony(modId);
        HarmonyMethod reinforcePrefix = new HarmonyMethod(typeof(RentalPermissionModSystem), nameof(RentalPermissionModSystem.OnStrengthenBlockPrefix))
        {
            priority = Priority.First
        };
        HarmonyMethod reinforcePostfix = new HarmonyMethod(typeof(RentalPermissionModSystem), nameof(RentalPermissionModSystem.OnStrengthenBlockPostfix));
        HarmonyMethod lockPrefix = new HarmonyMethod(typeof(RentalPermissionModSystem), nameof(RentalPermissionModSystem.OnTryLockPrefix))
        {
            priority = Priority.First
        };
        HarmonyMethod lockPostfix = new HarmonyMethod(typeof(RentalPermissionModSystem), nameof(RentalPermissionModSystem.OnTryLockPostfix));
        HarmonyMethod removeReinforcementPostfix = new HarmonyMethod(typeof(RentalPermissionModSystem), nameof(RentalPermissionModSystem.OnTryRemoveReinforcementPostfix));
        HarmonyMethod itemReinforcePrefix = new HarmonyMethod(typeof(RentalPermissionModSystem), nameof(RentalPermissionModSystem.OnPlumbAndSquareInteractPrefix))
        {
            priority = Priority.First
        };
        HarmonyMethod itemRemoveReinforcementPrefix = new HarmonyMethod(typeof(RentalPermissionModSystem), nameof(RentalPermissionModSystem.OnPlumbAndSquareAttackPrefix))
        {
            priority = Priority.First
        };
        HarmonyMethod itemLockPrefix = new HarmonyMethod(typeof(RentalPermissionModSystem), nameof(RentalPermissionModSystem.OnPadlockInteractPrefix))
        {
            priority = Priority.First
        };

        harmony.Patch(
            AccessTools.Method(typeof(ModSystemBlockReinforcement), nameof(ModSystemBlockReinforcement.StrengthenBlock)),
            prefix: reinforcePrefix,
            postfix: reinforcePostfix);
        harmony.Patch(
            AccessTools.Method(typeof(ModSystemBlockReinforcement), nameof(ModSystemBlockReinforcement.TryLock)),
            prefix: lockPrefix,
            postfix: lockPostfix);
        harmony.Patch(
            AccessTools.Method(typeof(ModSystemBlockReinforcement), nameof(ModSystemBlockReinforcement.TryRemoveReinforcement)),
            postfix: removeReinforcementPostfix);
        harmony.Patch(
            AccessTools.Method(typeof(ItemPlumbAndSquare), nameof(ItemPlumbAndSquare.OnHeldInteractStart)),
            prefix: itemReinforcePrefix);
        harmony.Patch(
            AccessTools.Method(typeof(ItemPlumbAndSquare), nameof(ItemPlumbAndSquare.OnHeldAttackStart)),
            prefix: itemRemoveReinforcementPrefix);
        harmony.Patch(
            AccessTools.Method(typeof(ItemPadlock), nameof(ItemPadlock.OnHeldInteractStart)),
            prefix: itemLockPrefix);

        return harmony;
    }
}
