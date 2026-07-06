using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace RentalPermission;

public class GuiDialogRentalPrompt : GuiDialog
{
    private const string DescriptionKey = "description";

    private readonly IClientNetworkChannel channel;
    private readonly RentalPromptPacket prompt;
    private readonly double[] durationOptions;
    private readonly string[] durationLabels;
    private double selectedDurationHours;

    public GuiDialogRentalPrompt(ICoreClientAPI capi, IClientNetworkChannel channel, RentalPromptPacket prompt) : base(capi)
    {
        this.channel = channel;
        this.prompt = prompt;
        durationOptions = BuildDurationOptions();
        durationLabels = BuildDurationLabels();
        selectedDurationHours = NormalizeDuration(prompt.SelectedDurationHours <= 0 ? prompt.MaxDurationHours : prompt.SelectedDurationHours);
        if (IsProtectionRemovalPrompt())
        {
            ComposeProtectionRemovalDialog();
        }
        else
        {
            ComposeDialog();
        }
    }

    public override string ToggleKeyCombinationCode => "rentalpermissionprompt";

    private void ComposeDialog()
    {
        CairoFont titleFont = CairoFont.WhiteDetailText().WithFontSize(15f);
        CairoFont textFont = CairoFont.WhiteDetailText().WithFontSize(13f);
        CairoFont smallFont = CairoFont.WhiteDetailText().WithFontSize(12f);
        CairoFont buttonFont = CairoFont.WhiteDetailText().WithFontSize(13f).WithOrientation(EnumTextOrientation.Center);

        string[] durationCodes = durationOptions.Select(h => h.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        int selectedIndex = Math.Max(0, Array.FindIndex(durationOptions, h => Math.Abs(h - selectedDurationHours) < 0.001));

        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        SingleComposer = capi.Gui.CreateCompo("rentalpermissionprompt", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Tr("rentalpermission-ui-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddStaticText(Tr("rentalpermission-ui-heading"), titleFont, ElementBounds.Fixed(0, 34, 390, 24))
                .AddStaticText(MainText(), textFont, ElementBounds.Fixed(0, 72, 390, 44))

                .AddStaticText(Tr("rentalpermission-ui-description-label"), smallFont, ElementBounds.Fixed(0, 124, 160, 20))
                .AddTextInput(ElementBounds.Fixed(162, 118, 260, 26), null, textFont, DescriptionKey)

                .AddStaticText(Tr("rentalpermission-ui-rate", prompt.Price, prompt.CurrencyName, FormatDurationForHours(prompt.MaxDurationHours)), smallFont, ElementBounds.Fixed(0, 162, 430, 22))
                .AddStaticText(Tr("rentalpermission-ui-duration-label"), smallFont, ElementBounds.Fixed(0, 198, 120, 20))
                .AddDropDown(durationCodes, durationLabels, selectedIndex, OnDurationChanged, ElementBounds.Fixed(126, 192, 180, 26), smallFont, "duration")

                .AddDynamicText(CalculationText(), textFont, ElementBounds.Fixed(0, 238, 430, 42), "calculation")
                .AddStaticText(Tr("rentalpermission-ui-expiration-warning"), smallFont, ElementBounds.Fixed(0, 286, 430, 42))

                .AddButton(Tr("rentalpermission-ui-cancel"), Cancel, ElementBounds.Fixed(204, 338, 95, 28), buttonFont, EnumButtonStyle.Small)
                .AddButton(prompt.IsRenewal ? Tr("rentalpermission-ui-renew") : Tr("rentalpermission-ui-rent"), Confirm, ElementBounds.Fixed(312, 338, 110, 28), buttonFont, EnumButtonStyle.Small)
            .EndChildElements()
            .Compose();

        if (!string.IsNullOrWhiteSpace(prompt.ExistingDescription))
        {
            SingleComposer.GetTextInput(DescriptionKey)?.SetValue(prompt.ExistingDescription);
        }
    }

    private void ComposeProtectionRemovalDialog()
    {
        CairoFont titleFont = CairoFont.WhiteDetailText().WithFontSize(15f);
        CairoFont textFont = CairoFont.WhiteDetailText().WithFontSize(13f);
        CairoFont buttonFont = CairoFont.WhiteDetailText().WithFontSize(13f).WithOrientation(EnumTextOrientation.Center);

        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        SingleComposer = capi.Gui.CreateCompo("rentalpermissionprompt", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Tr("rentalpermission-ui-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddStaticText(Tr("rentalpermission-ui-remove-heading"), titleFont, ElementBounds.Fixed(0, 34, 430, 24))
                .AddStaticText(Tr("rentalpermission-ui-remove-main", prompt.BlockName), textFont, ElementBounds.Fixed(0, 76, 430, 78))
                .AddStaticText(Tr("rentalpermission-ui-remove-warning"), textFont, ElementBounds.Fixed(0, 164, 430, 52))
                .AddButton(Tr("rentalpermission-ui-cancel"), Cancel, ElementBounds.Fixed(204, 236, 95, 28), buttonFont, EnumButtonStyle.Small)
                .AddButton(Tr("rentalpermission-ui-remove-confirm"), Confirm, ElementBounds.Fixed(312, 236, 110, 28), buttonFont, EnumButtonStyle.Small)
            .EndChildElements()
            .Compose();
    }

    private void OnDurationChanged(string code, bool selected)
    {
        if (!selected)
        {
            return;
        }

        if (double.TryParse(code, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hours))
        {
            selectedDurationHours = NormalizeDuration(hours);
            RefreshTexts();
        }
    }

    private void RefreshTexts()
    {
        SingleComposer.GetDynamicText("calculation").SetNewText(CalculationText());
    }

    private string MainText()
    {
        string action = prompt.ActionType.Equals("Lock", StringComparison.OrdinalIgnoreCase)
            ? Tr("rentalpermission-ui-action-lock")
            : Tr("rentalpermission-ui-action-reinforce");
        return prompt.IsRenewal
            ? Tr("rentalpermission-ui-main-renewal")
            : Tr("rentalpermission-ui-main", action);
    }

    private bool IsProtectionRemovalPrompt()
    {
        return prompt.PromptKind.Equals("RemoveProtection", StringComparison.OrdinalIgnoreCase);
    }

    private string CalculationText()
    {
        return Tr("rentalpermission-ui-calculation", FormatDurationForHours(selectedDurationHours), CurrentPrice(), prompt.CurrencyName);
    }

    private int CurrentPrice()
    {
        if (prompt.Price <= 0 || prompt.MaxDurationHours <= 0 || selectedDurationHours <= 0)
        {
            return prompt.Price;
        }

        return Math.Max(1, (int)Math.Ceiling(prompt.Price * selectedDurationHours / prompt.MaxDurationHours));
    }

    private double[] BuildDurationOptions()
    {
        if (prompt.MaxDurationHours <= 0)
        {
            return new[] { 0d };
        }

        double min = prompt.MinDurationHours > 0 ? Math.Min(prompt.MinDurationHours, prompt.MaxDurationHours) : prompt.MaxDurationHours;
        double step = prompt.DurationStepHours > 0 ? prompt.DurationStepHours : prompt.MaxDurationHours - min;
        if (step <= 0 || min >= prompt.MaxDurationHours)
        {
            return new[] { prompt.MaxDurationHours };
        }

        List<double> values = new();
        for (double current = min; current < prompt.MaxDurationHours; current += step)
        {
            values.Add(Math.Round(current, 4));
            if (values.Count >= 100)
            {
                break;
            }
        }

        if (values.Count == 0 || Math.Abs(values[^1] - prompt.MaxDurationHours) > 0.001)
        {
            values.Add(Math.Round(prompt.MaxDurationHours, 4));
        }

        return values.ToArray();
    }

    private string[] BuildDurationLabels()
    {
        if (durationOptions.Length == 0)
        {
            return Array.Empty<string>();
        }

        double minValue = prompt.MinDurationValue > 0 ? prompt.MinDurationValue : prompt.MaxDurationValue;
        double maxValue = prompt.MaxDurationValue > 0 ? prompt.MaxDurationValue : minValue;
        double stepValue = prompt.DurationStepValue > 0 ? prompt.DurationStepValue : maxValue - minValue;
        if (stepValue <= 0 || minValue >= maxValue)
        {
            return new[] { FormatDurationValue(maxValue, prompt.DurationUnit) };
        }

        List<string> labels = new();
        for (double current = minValue; current < maxValue; current += stepValue)
        {
            labels.Add(FormatDurationValue(current, prompt.DurationUnit));
            if (labels.Count >= durationOptions.Length)
            {
                break;
            }
        }

        if (labels.Count < durationOptions.Length)
        {
            labels.Add(FormatDurationValue(maxValue, prompt.DurationUnit));
        }

        while (labels.Count < durationOptions.Length)
        {
            labels.Add(FormatDurationForHours(durationOptions[labels.Count]));
        }

        return labels.ToArray();
    }

    private double NormalizeDuration(double value)
    {
        if (durationOptions.Length == 0)
        {
            return prompt.MaxDurationHours;
        }

        return durationOptions
            .OrderBy(option => Math.Abs(option - value))
            .First();
    }

    private string FormatDurationForHours(double hours)
    {
        int index = Array.FindIndex(durationOptions, option => Math.Abs(option - hours) < 0.001);
        return index >= 0 && index < durationLabels.Length
            ? durationLabels[index]
            : FormatDuration(hours);
    }

    private static string FormatDuration(double hours)
    {
        if (hours <= 0)
        {
            return Tr("rentalpermission-duration-never");
        }

        if (hours >= 24 && Math.Abs(hours % 24) < 0.001)
        {
            return Tr("rentalpermission-duration-days", $"{hours / 24:0.#}");
        }

        return Tr("rentalpermission-duration-hours", $"{hours:0.#}");
    }

    private static string FormatDurationValue(double value, string unit)
    {
        if (value <= 0)
        {
            return Tr("rentalpermission-duration-never");
        }

        string normalized = (unit ?? "hours").Trim().ToLowerInvariant();
        return normalized switch
        {
            "days" => Tr("rentalpermission-duration-days", $"{value:0.#}"),
            "months" => Tr("rentalpermission-duration-months", $"{value:0.#}"),
            "years" => Tr("rentalpermission-duration-years", $"{value:0.#}"),
            _ => Tr("rentalpermission-duration-hours", $"{value:0.#}")
        };
    }

    private static string Tr(string key, params object[] args)
    {
        return Lang.Get(key.Contains(':') ? key : $"rentalpermission:{key}", args);
    }

    private bool Confirm()
    {
        if (IsProtectionRemovalPrompt())
        {
            channel.SendPacket(new RentalConfirmPacket { RequestId = prompt.RequestId });
            TryClose();
            return true;
        }

        string description = SingleComposer.GetTextInput(DescriptionKey)?.GetText()?.Trim() ?? string.Empty;
        if (description.Length == 0)
        {
            capi.TriggerIngameError(this, "rentalpermission-description-required", Tr("rentalpermission-ui-description-required"));
            return true;
        }

        channel.SendPacket(new RentalConfirmPacket { RequestId = prompt.RequestId, SelectedDurationHours = selectedDurationHours, Description = description });
        TryClose();
        return true;
    }

    private bool Cancel()
    {
        channel.SendPacket(new RentalCancelPacket { RequestId = prompt.RequestId });
        TryClose();
        return true;
    }

    private void OnTitleBarClose()
    {
        Cancel();
    }
}
