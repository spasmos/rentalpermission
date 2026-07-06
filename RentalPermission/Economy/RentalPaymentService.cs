using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class RentalPaymentService
{
    private readonly Func<RentalPermissionConfig> getConfig;
    private readonly RentalTextFormatter textFormatter;
    private readonly CurrencyWallet currencyWallet;

    public RentalPaymentService(Func<RentalPermissionConfig> getConfig, RentalTextFormatter textFormatter, CurrencyWallet currencyWallet)
    {
        this.getConfig = getConfig;
        this.textFormatter = textFormatter;
        this.currencyWallet = currencyWallet;
    }

    public bool TryCharge(IServerPlayer player, int amount, out string error)
    {
        error = string.Empty;
        if (amount <= 0)
        {
            return true;
        }

        int available = CountCurrency(player);
        if (available < amount)
        {
            error = textFormatter.T(player, "rentalpermission-payment-required", amount, GetCurrencyDisplayName(player), available);
            return false;
        }

        return currencyWallet.TryTake(player, amount);
    }

    public int CountCurrency(IServerPlayer player)
    {
        return currencyWallet.Count(player);
    }

    public void Refund(IServerPlayer player, int amount)
    {
        currencyWallet.Refund(player, amount);
    }

    public string GetCurrencyDisplayName(IServerPlayer player)
    {
        return RentalTextFormatter.GetCollectibleDisplayName(player.LanguageCode, getConfig().CurrencyItemCode);
    }
}
