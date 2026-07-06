namespace RentalPermission;

internal sealed class RentalDurationCalculator
{
    private readonly Func<RentalPermissionConfig> getConfig;

    public RentalDurationCalculator(Func<RentalPermissionConfig> getConfig)
    {
        this.getConfig = getConfig;
    }

    public double NormalizeMinDuration(RentalRule rule)
    {
        double maxDuration = GetMaxDurationHours(rule);
        if (maxDuration <= 0)
        {
            return 0;
        }

        double minDuration = ConvertDurationToHours(rule.MinRentDuration, rule.RentDurationUnit);
        return minDuration > 0
            ? Math.Min(minDuration, maxDuration)
            : maxDuration;
    }

    public static double NormalizeMinDurationValue(RentalRule rule)
    {
        if (rule.RentDuration <= 0)
        {
            return 0;
        }

        return rule.MinRentDuration > 0
            ? Math.Min(rule.MinRentDuration, rule.RentDuration)
            : rule.RentDuration;
    }

    public double NormalizeDurationStep(RentalRule rule, double minDuration, double maxDuration)
    {
        if (maxDuration <= 0 || minDuration >= maxDuration)
        {
            return 0;
        }

        double step = ConvertDurationToHours(rule.RentDurationStep, rule.RentDurationUnit);
        return step > 0
            ? Math.Min(step, maxDuration - minDuration)
            : maxDuration - minDuration;
    }

    public double NormalizeRequestedDuration(RentalRule rule, double requestedDurationHours)
    {
        double maxDuration = GetMaxDurationHours(rule);
        if (maxDuration <= 0)
        {
            return 0;
        }

        double minDuration = NormalizeMinDuration(rule);
        double step = NormalizeDurationStep(rule, minDuration, maxDuration);
        double requested = requestedDurationHours > 0 ? requestedDurationHours : maxDuration;
        double clamped = Math.Max(minDuration, Math.Min(maxDuration, requested));
        if (step > 0)
        {
            double steps = Math.Round((clamped - minDuration) / step);
            clamped = minDuration + steps * step;
        }

        return Math.Round(Math.Max(minDuration, Math.Min(maxDuration, clamped)), 4);
    }

    public double GetMaxDurationHours(RentalRule rule)
    {
        return Math.Max(0, ConvertDurationToHours(rule.RentDuration, rule.RentDurationUnit));
    }

    public double ConvertDurationToHours(double value, string? unit)
    {
        if (value <= 0)
        {
            return 0;
        }

        return NormalizeDurationUnit(unit) switch
        {
            "days" => value * 24,
            "months" => value * ValidDaysPerMonth() * 24,
            "years" => value * ValidDaysPerMonth() * ValidMonthsPerYear() * 24,
            _ => value
        };
    }

    public static string NormalizeDurationUnit(string? unit)
    {
        string normalized = (unit ?? "hours").Trim().ToLowerInvariant();
        return normalized is "hours" or "days" or "months" or "years"
            ? normalized
            : "hours";
    }

    private double ValidDaysPerMonth()
    {
        return getConfig().DaysPerMonth > 0 ? getConfig().DaysPerMonth : 30;
    }

    private double ValidMonthsPerYear()
    {
        return getConfig().MonthsPerYear > 0 ? getConfig().MonthsPerYear : 12;
    }
}
