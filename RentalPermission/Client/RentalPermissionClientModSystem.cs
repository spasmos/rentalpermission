using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RentalPermission;

public class RentalPermissionClientModSystem : ModSystem
{
    private ICoreClientAPI? capi;
    private IClientNetworkChannel? channel;
    private GuiDialogRentalPrompt? dialog;

    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Client;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        channel = api.Network.RegisterChannel("rentalpermission")
            .RegisterMessageType<RentalPromptPacket>()
            .RegisterMessageType<RentalConfirmPacket>()
            .RegisterMessageType<RentalCancelPacket>()
            .SetMessageHandler<RentalPromptPacket>(OnRentalPrompt);
    }

    public override void Dispose()
    {
        dialog?.TryClose();
        dialog?.Dispose();
        dialog = null;
        channel = null;
        capi = null;
    }

    private void OnRentalPrompt(RentalPromptPacket packet)
    {
        if (capi == null || channel == null)
        {
            return;
        }

        dialog?.TryClose();
        dialog?.Dispose();
        dialog = new GuiDialogRentalPrompt(capi, channel, packet);
        dialog.TryOpen();
    }
}
