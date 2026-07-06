using System.Text.Json;
using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class RentalPermissionStore
{
    private const string ConfigName = "rentalpermission.json";
    private const string ModDataFolderName = "ModData";
    private const string ModFolderName = "rentalpermission";
    private const string StateFileName = "rentalpermission.state.json";

    private readonly Func<ICoreServerAPI?> getServerApi;
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
    private string stateFilePath = string.Empty;

    public RentalPermissionStore(Func<ICoreServerAPI?> getServerApi)
    {
        this.getServerApi = getServerApi;
    }

    public RentalPermissionSnapshot Load()
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null)
        {
            return new RentalPermissionSnapshot(RentalPermissionConfig.CreateDefault(), new RentalPermissionData());
        }

        RentalPermissionConfig config = sapi.LoadModConfig<RentalPermissionConfig>(ConfigName) ?? RentalPermissionConfig.CreateDefault();
        if (config.Claims.Length == 0)
        {
            config = RentalPermissionConfig.CreateDefault();
        }
        sapi.StoreModConfig(config, ConfigName);

        RentalPermissionData data = LoadData(sapi);
        NormalizeData(data);
        SaveData(data);

        return new RentalPermissionSnapshot(config, data);
    }

    public void SaveData(RentalPermissionData data)
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(stateFilePath))
        {
            stateFilePath = GetStateFilePath(sapi);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
        File.WriteAllText(stateFilePath, JsonSerializer.Serialize(data, jsonOptions));
    }

    private RentalPermissionData LoadData(ICoreServerAPI sapi)
    {
        stateFilePath = GetStateFilePath(sapi);
        if (!File.Exists(stateFilePath))
        {
            return new RentalPermissionData();
        }

        try
        {
            return JsonSerializer.Deserialize<RentalPermissionData>(File.ReadAllText(stateFilePath), jsonOptions)
                ?? new RentalPermissionData();
        }
        catch (Exception ex)
        {
            sapi.Logger.Warning(
                "[RentalPermission] Could not load rental state from {0}: {1}. Starting with empty state.",
                stateFilePath,
                ex.Message);
            return new RentalPermissionData();
        }
    }

    private static void NormalizeData(RentalPermissionData data)
    {
        foreach (RentalRecord record in data.Rentals)
        {
            if (string.IsNullOrWhiteSpace(record.Id))
            {
                record.Id = RentalRecordTools.CreateRentalId();
            }

            if (string.IsNullOrWhiteSpace(record.Status))
            {
                record.Status = RentalStatus.Active;
            }
        }
    }

    private static string GetStateFilePath(ICoreServerAPI sapi)
    {
        string savegameIdentifier = sapi.WorldManager.SaveGame.SavegameIdentifier;
        if (string.IsNullOrWhiteSpace(savegameIdentifier))
        {
            savegameIdentifier = "unknown-world";
        }

        string modDataPath = sapi.GetOrCreateDataPath(ModDataFolderName);
        string folder = Path.Combine(modDataPath, savegameIdentifier, ModFolderName);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, StateFileName);
    }
}
