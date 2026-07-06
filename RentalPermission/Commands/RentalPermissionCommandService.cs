using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class RentalPermissionCommandService
{
    private readonly Func<ICoreServerAPI?> getServerApi;
    private readonly Func<RentalPermissionConfig> getConfig;
    private readonly Func<RentalPermissionData> getData;
    private readonly Action loadAll;
    private readonly Action saveData;
    private readonly System.Func<string, int> processExpiredRentals;
    private readonly RentalLedger ledger;
    private readonly RentalRuleResolver ruleResolver;
    private readonly RentalTextFormatter textFormatter;

    public RentalPermissionCommandService(
        Func<ICoreServerAPI?> getServerApi,
        Func<RentalPermissionConfig> getConfig,
        Func<RentalPermissionData> getData,
        Action loadAll,
        Action saveData,
        System.Func<string, int> processExpiredRentals,
        RentalLedger ledger,
        RentalRuleResolver ruleResolver,
        RentalTextFormatter textFormatter)
    {
        this.getServerApi = getServerApi;
        this.getConfig = getConfig;
        this.getData = getData;
        this.loadAll = loadAll;
        this.saveData = saveData;
        this.processExpiredRentals = processExpiredRentals;
        this.ledger = ledger;
        this.ruleResolver = ruleResolver;
        this.textFormatter = textFormatter;
    }

    public void Register(ICoreServerAPI api)
    {
        RentalPermissionCommandRegistrar.Register(api, new RentalPermissionCommandHandlers
        {
            Reload = args => RunCommand(args, "reload", OnReloadCommand),
            List = args => RunCommand(args, "list", OnListCommand),
            Mine = args => RunCommand(args, "mine", OnMineCommand),
            Renew = args => RunCommand(args, "renew", OnRenewCommand),
            Cancel = args => RunCommand(args, "cancel", OnCancelCommand),
            Process = args => RunCommand(args, "process", OnProcessCommand),
            Here = args => RunCommand(args, "here", OnHereCommand)
        });
    }

    private TextCommandResult RunCommand(TextCommandCallingArgs args, string commandName, System.Func<TextCommandCallingArgs, TextCommandResult> handler)
    {
        try
        {
            return handler(args);
        }
        catch (Exception exception)
        {
            RentalPermissionConfig config = getConfig();
            if (config.LogRentalEvents)
            {
                getServerApi()?.Logger.Error("[RentalPermission] Command /rentalpermission {0} failed: {1}", commandName, exception);
            }

            return TextCommandResult.Error(textFormatter.TL(RentalTextFormatter.CallerLang(args), "rentalpermission-command-failed", commandName));
        }
    }

    private TextCommandResult OnReloadCommand(TextCommandCallingArgs args)
    {
        loadAll();
        return TextCommandResult.Success(textFormatter.TL(RentalTextFormatter.CallerLang(args), "rentalpermission-reloaded"));
    }

    private TextCommandResult OnListCommand(TextCommandCallingArgs args)
    {
        string langCode = RentalTextFormatter.CallerLang(args);
        List<RentalRecord> rentals = getData().Rentals ?? new List<RentalRecord>();
        if (rentals.Count == 0)
        {
            return TextCommandResult.Success(textFormatter.TL(langCode, "rentalpermission-list-no-rentals"));
        }

        return TextCommandResult.Success(FormatRentalList(rentals, 50, langCode));
    }

    private TextCommandResult OnMineCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is not IServerPlayer player)
        {
            return TextCommandResult.Error(textFormatter.TL(RentalTextFormatter.CallerLang(args), "rentalpermission-player-command-only"));
        }

        List<RentalRecord> rentals = (getData().Rentals ?? new List<RentalRecord>())
            .Where(record => record.PlayerUID == player.PlayerUID)
            .ToList();
        if (rentals.Count == 0)
        {
            return TextCommandResult.Success(textFormatter.T(player, "rentalpermission-list-no-player-rentals"));
        }

        return TextCommandResult.Success(FormatRentalList(rentals, 20, player.LanguageCode));
    }

    private TextCommandResult OnRenewCommand(TextCommandCallingArgs args)
    {
        return TextCommandResult.Error(textFormatter.TL(RentalTextFormatter.CallerLang(args), "rentalpermission-renew-in-world-only"));
    }

    private TextCommandResult OnCancelCommand(TextCommandCallingArgs args)
    {
        string langCode = RentalTextFormatter.CallerLang(args);
        string id = args[0] as string ?? string.Empty;
        RentalRecord? record = ledger.FindRental(id);
        if (record == null)
        {
            return TextCommandResult.Error(textFormatter.TL(langCode, "rentalpermission-rental-not-found", id));
        }

        record.Status = RentalStatus.Cancelled;
        record.ProcessedAtTotalSeconds = getServerApi() == null ? 0 : (long)getServerApi()!.World.Calendar.ElapsedSeconds;
        record.ExpirationResult = "cancelled by admin";
        saveData();
        return TextCommandResult.Success(textFormatter.TL(langCode, "rentalpermission-cancelled", record.Id));
    }

    private TextCommandResult OnProcessCommand(TextCommandCallingArgs args)
    {
        int processed = processExpiredRentals("manual");
        return TextCommandResult.Success(textFormatter.TL(RentalTextFormatter.CallerLang(args), "rentalpermission-processed", processed));
    }

    private TextCommandResult OnHereCommand(TextCommandCallingArgs args)
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null || args.Caller.Player is not IServerPlayer player)
        {
            return TextCommandResult.Error(textFormatter.TL(RentalTextFormatter.CallerLang(args), "rentalpermission-player-command-only"));
        }

        BlockPos pos = args.Caller.Pos.AsBlockPos;
        LandClaim[] claims = sapi.World.Claims.Get(pos) ?? Array.Empty<LandClaim>();
        if (claims.Length == 0)
        {
            return TextCommandResult.Success($"No claim found at {PosKey(pos)}.");
        }

        string claimLines = string.Join("\n", claims.Select((claim, index) =>
            $"{index}: claimId={ruleResolver.GetOwnerClaimId(claim)}, owner='{claim.LastKnownOwnerName ?? claim.OwnedByPlayerUid ?? claim.OwnedByPlayerGroupUid.ToString()}', description='{claim.Description ?? string.Empty}'"));
        RentalPermissionConfig config = getConfig();
        string matchingRules = string.Join(", ", config.Claims.Where(rule => rule.Enabled && ruleResolver.ClaimsMatch(rule, claims)).Select(rule => rule.Name));
        if (string.IsNullOrWhiteSpace(matchingRules))
        {
            matchingRules = textFormatter.TL(player.LanguageCode, "rentalpermission-none");
        }

        return TextCommandResult.Success($"Claims at {PosKey(pos)}:\n{claimLines}\nMatching rental claim rules: {matchingRules}");
    }

    private string FormatRentalList(IEnumerable<RentalRecord> rentals, int max, string langCode)
    {
        RentalPermissionConfig config = getConfig();
        long now = getServerApi() == null ? 0 : (long)getServerApi()!.World.Calendar.ElapsedSeconds;
        List<string> lines = rentals
            .Where(record => record != null)
            .OrderBy(record => record.IsActive() ? 0 : 1)
            .ThenBy(record => record.ExpiresAtTotalSeconds <= 0 ? long.MaxValue : record.ExpiresAtTotalSeconds)
            .Take(max)
            .Select(record =>
            {
                string status = record.StatusOrActive();
                string expires;
                if (record.ExpiresAtTotalSeconds <= 0)
                {
                    expires = textFormatter.TL(langCode, "rentalpermission-list-expires-never");
                }
                else if (record.ExpiresAtTotalSeconds <= now)
                {
                    long overdue = Math.Max(0, now - record.ExpiresAtTotalSeconds);
                    expires = textFormatter.TL(langCode, "rentalpermission-list-expired-ago", textFormatter.FormatGameTime(langCode, overdue));
                    if (record.IsActive())
                    {
                        status = "ExpiredPendingProcessing";
                    }
                }
                else
                {
                    expires = textFormatter.FormatGameTime(langCode, record.ExpiresAtTotalSeconds - now);
                }

                return textFormatter.TL(
                    langCode,
                    "rentalpermission-list-row",
                    RentalTextFormatter.DisplayOrFallback(record.Id, "?"),
                    status,
                    RentalTextFormatter.DisplayOrFallback(record.PlayerName, "?"),
                    string.IsNullOrWhiteSpace(record.Description) ? textFormatter.GetRentalBlockDisplayName(record, langCode) : record.Description,
                    RentalTextFormatter.DisplayOrFallback(record.PositionKey, $"{record.Dimension}/{record.X}/{record.Y}/{record.Z}"),
                    RentalTextFormatter.DisplayOrFallback(record.RuleName, "?"),
                    record.PaidAmount,
                    RentalTextFormatter.GetCollectibleDisplayName(langCode, string.IsNullOrWhiteSpace(record.CurrencyItemCode) ? config.CurrencyItemCode : record.CurrencyItemCode),
                    expires);
            })
            .ToList();
        return string.Join("\n", lines);
    }

    private static string PosKey(BlockPos pos)
    {
        return $"{pos.dimension}/{pos.X}/{pos.Y}/{pos.Z}";
    }
}
