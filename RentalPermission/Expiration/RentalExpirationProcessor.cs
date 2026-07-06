using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class RentalExpirationProcessor
{
    private readonly Func<ICoreServerAPI?> getServerApi;
    private readonly Func<RentalPermissionConfig> getConfig;
    private readonly Func<RentalPermissionData> getData;
    private readonly RentalExpirationActionExecutor actionExecutor;
    private readonly Action saveData;
    private readonly Action<RentalRecord, long> notifyRenewalApplied;

    public RentalExpirationProcessor(
        Func<ICoreServerAPI?> getServerApi,
        Func<RentalPermissionConfig> getConfig,
        Func<RentalPermissionData> getData,
        RentalExpirationActionExecutor actionExecutor,
        Action saveData,
        Action<RentalRecord, long> notifyRenewalApplied)
    {
        this.getServerApi = getServerApi;
        this.getConfig = getConfig;
        this.getData = getData;
        this.actionExecutor = actionExecutor;
        this.saveData = saveData;
        this.notifyRenewalApplied = notifyRenewalApplied;
    }

    public long Register(ICoreServerAPI api, Func<string, int> processExpiredRentals)
    {
        RentalPermissionConfig config = getConfig();
        if (!config.EnableExpirationProcessing)
        {
            return 0;
        }

        int interval = Math.Max(5, config.ExpirationCheckIntervalSeconds) * 1000;
        return api.Event.RegisterGameTickListener(_ => processExpiredRentals("scheduled"), interval, interval);
    }

    public int Process(string reason)
    {
        ICoreServerAPI? sapi = getServerApi();
        if (sapi == null)
        {
            return 0;
        }

        RentalPermissionConfig config = getConfig();
        RentalPermissionData data = getData();
        long now = (long)sapi.World.Calendar.ElapsedSeconds;
        RentalRecord[] expired = data.Rentals
            .Where(record => record.IsActive() && record.ExpiresAtTotalSeconds > 0 && record.ExpiresAtTotalSeconds <= now)
            .OrderBy(record => record.ExpiresAtTotalSeconds)
            .Take(Math.Max(1, config.MaxExpirationsPerCheck))
            .ToArray();

        foreach (RentalRecord record in expired)
        {
            if (TryApplyPendingRenewal(record, now, reason))
            {
                continue;
            }

            string result = actionExecutor.Execute(record);
            record.Status = RentalStatus.Expired;
            record.ProcessedAtTotalSeconds = now;
            record.ExpirationResult = $"{reason}: {result}";

            if (config.LogRentalEvents)
            {
                sapi.Logger.Notification("[RentalPermission] Expired rental {0} at {1}: {2}", record.Id, record.PositionKey, result);
            }
        }

        int deleted = DeleteProcessedRentals(now);

        if (expired.Length > 0 || deleted > 0)
        {
            saveData();
        }

        return expired.Length;
    }

    private bool TryApplyPendingRenewal(RentalRecord record, long now, string reason)
    {
        if (record.PendingRenewalPaidAmount <= 0 || record.PendingRenewalDurationHours <= 0)
        {
            return false;
        }

        long renewFrom = Math.Max(record.ExpiresAtTotalSeconds, now);
        record.ExpiresAtTotalSeconds = renewFrom + (long)(record.PendingRenewalDurationHours * 3600);
        record.PaidAmount += record.PendingRenewalPaidAmount;
        record.RenewalCount++;
        record.LastRenewedAtTotalSeconds = now;
        record.PendingRenewalPaidAmount = 0;
        record.PendingRenewalDurationHours = 0;
        record.PendingRenewalCreatedAtTotalSeconds = 0;
        record.PendingRenewalCurrencyItemCode = string.Empty;
        record.Status = RentalStatus.Active;
        record.ProcessedAtTotalSeconds = 0;
        record.ExpirationResult = $"{reason}: renewed from prepaid renewal";

        notifyRenewalApplied(record, now);

        RentalPermissionConfig config = getConfig();
        ICoreServerAPI? sapi = getServerApi();
        if (config.LogRentalEvents && sapi != null)
        {
            sapi.Logger.Notification("[RentalPermission] Applied pending renewal for rental {0} at {1}. New expiration: {2}", record.Id, record.PositionKey, record.ExpiresAtTotalSeconds);
        }

        return true;
    }

    private int DeleteProcessedRentals(long now)
    {
        RentalPermissionConfig config = getConfig();
        if (config.DeleteProcessedRentalsAfterHours < 0)
        {
            return 0;
        }

        RentalPermissionData data = getData();
        long keepSeconds = (long)(config.DeleteProcessedRentalsAfterHours * 3600);
        int before = data.Rentals.Count;
        data.Rentals.RemoveAll(record =>
            !record.IsActive()
            && record.ProcessedAtTotalSeconds > 0
            && now - record.ProcessedAtTotalSeconds >= keepSeconds);
        return before - data.Rentals.Count;
    }
}
