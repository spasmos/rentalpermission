using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class RentalTextFormatter
{
    private readonly Func<ICoreServerAPI?> getServerApi;

    public RentalTextFormatter(Func<ICoreServerAPI?> getServerApi)
    {
        this.getServerApi = getServerApi;
    }

    public string T(IServerPlayer player, string key, params object[] args)
    {
        return TL(player.LanguageCode, key, args);
    }

    public string TL(string langCode, string key, params object[] args)
    {
        string normalizedLang = string.IsNullOrWhiteSpace(langCode) ? "en" : langCode;
        string modKey = ModLangKey(key);
        string translated = Lang.GetL(normalizedLang, modKey, args);
        if (!IsMissingTranslation(translated, modKey))
        {
            return translated;
        }

        translated = Lang.GetL(normalizedLang, key, args);
        return IsMissingTranslation(translated, key) ? key : translated;
    }

    public static string CallerLang(TextCommandCallingArgs args)
    {
        return args.Caller.Player is IServerPlayer player ? player.LanguageCode : "en";
    }

    public string FormatDuration(IServerPlayer player, double hours)
    {
        return FormatDuration(player.LanguageCode, hours);
    }

    public string FormatDuration(string langCode, double hours)
    {
        if (hours <= 0)
        {
            return TL(langCode, "rentalpermission-duration-never");
        }

        if (hours >= 24 && Math.Abs(hours % 24) < 0.001)
        {
            return TL(langCode, "rentalpermission-duration-days", $"{hours / 24:0.#}");
        }

        return TL(langCode, "rentalpermission-duration-hours", $"{hours:0.#}");
    }

    public string GetBlockDisplayName(IServerPlayer player, Block block, BlockPos? pos = null)
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi != null && pos != null)
        {
            try
            {
                string placedName = block.GetPlacedBlockName(sapi.World, pos);
                if (!string.IsNullOrWhiteSpace(placedName) && !LooksLikeAssetCode(placedName))
                {
                    return placedName;
                }
            }
            catch
            {
                // Fall back to language keys below.
            }
        }

        return GetCollectibleDisplayName(player.LanguageCode, block.Code?.ToString() ?? string.Empty, blockPreferred: true);
    }

    public string GetRentalBlockDisplayName(RentalRecord record, string? langCode = null)
    {
        if (!string.IsNullOrWhiteSpace(record.Description))
        {
            return record.Description;
        }

        if (!string.IsNullOrWhiteSpace(record.BlockName) && !LooksLikeAssetCode(record.BlockName))
        {
            return record.BlockName;
        }

        return GetCollectibleDisplayName(langCode ?? "en", record.BlockCode, blockPreferred: true);
    }

    public string FormatGameTime(string langCode, long seconds)
    {
        if (seconds < 60)
        {
            return TL(langCode, "rentalpermission-time-seconds", seconds);
        }

        long minutes = seconds / 60;
        if (minutes < 60)
        {
            return TL(langCode, "rentalpermission-time-minutes-seconds", minutes, seconds % 60);
        }

        long hours = minutes / 60;
        long remainingMinutes = minutes % 60;
        if (hours < 24)
        {
            return TL(langCode, "rentalpermission-time-hours-minutes", hours, remainingMinutes);
        }

        long days = hours / 24;
        return TL(langCode, "rentalpermission-time-days-hours", days, hours % 24);
    }

    public static string GetCollectibleDisplayName(string langCode, string code, bool blockPreferred = false)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return code;
        }

        AssetLocation location;
        try
        {
            location = new AssetLocation(code);
        }
        catch
        {
            return code;
        }

        string[] keys = blockPreferred
            ? new[]
            {
                $"block-{location.Domain}-{location.Path}",
                $"block-{location.Path}",
                $"item-{location.Domain}-{location.Path}",
                $"item-{location.Path}"
            }
            : new[]
            {
                $"item-{location.Domain}-{location.Path}",
                $"item-{location.Path}",
                $"block-{location.Domain}-{location.Path}",
                $"block-{location.Path}"
            };

        foreach (string key in keys)
        {
            string translated = Lang.GetMatchingL(langCode, key);
            if (!string.IsNullOrWhiteSpace(translated) && !translated.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return translated;
            }
        }

        return code;
    }

    public static bool LooksLikeAssetCode(string value)
    {
        return value.Contains(':') || value.StartsWith("block-", StringComparison.OrdinalIgnoreCase) || value.StartsWith("item-", StringComparison.OrdinalIgnoreCase);
    }

    public static string DisplayOrFallback(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string ModLangKey(string key)
    {
        return key.Contains(':') ? key : $"rentalpermission:{key}";
    }

    private static bool IsMissingTranslation(string translated, string key)
    {
        return string.IsNullOrWhiteSpace(translated)
            || translated == key
            || translated == ModLangKey(key);
    }
}
