using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace RentalPermission;

internal sealed class CurrencyWallet
{
    private readonly Func<ICoreServerAPI?> getServerApi;
    private readonly Func<string> getCurrencyItemCode;

    public CurrencyWallet(Func<ICoreServerAPI?> getServerApi, Func<string> getCurrencyItemCode)
    {
        this.getServerApi = getServerApi;
        this.getCurrencyItemCode = getCurrencyItemCode;
    }

    public int Count(IServerPlayer player)
    {
        int count = 0;
        ((EntityAgent)player.Entity).WalkInventory(slot =>
        {
            if (slot.Itemstack == null || slot is ItemSlotCreative || slot.Inventory is not InventoryBasePlayer)
            {
                return true;
            }

            string code = slot.Itemstack.Collectible?.Code?.ToString() ?? string.Empty;
            if (code.Equals(getCurrencyItemCode(), StringComparison.OrdinalIgnoreCase))
            {
                count += slot.StackSize;
            }

            return true;
        });
        return count;
    }

    public bool TryTake(IServerPlayer player, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        int remaining = amount;
        ((EntityAgent)player.Entity).WalkInventory(slot =>
        {
            if (remaining <= 0 || slot.Itemstack == null || slot is ItemSlotCreative || slot.Inventory is not InventoryBasePlayer)
            {
                return remaining > 0;
            }

            string code = slot.Itemstack.Collectible?.Code?.ToString() ?? string.Empty;
            if (!code.Equals(getCurrencyItemCode(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int take = Math.Min(remaining, slot.StackSize);
            slot.TakeOut(take);
            slot.MarkDirty();
            remaining -= take;
            return remaining > 0;
        });

        return remaining == 0;
    }

    public void Refund(IServerPlayer player, int amount)
    {
        ICoreServerAPI? sapi = getServerApi();
        if (amount <= 0 || sapi == null)
        {
            return;
        }

        Item? item = sapi.World.GetItem(new AssetLocation(getCurrencyItemCode()));
        if (item == null)
        {
            return;
        }

        player.InventoryManager.TryGiveItemstack(new ItemStack(item, amount), true);
    }
}
