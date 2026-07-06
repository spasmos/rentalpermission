using Vintagestory.API.Server;

namespace RentalPermission;

internal sealed class RentalPromptService
{
    private readonly Dictionary<string, PendingRentalPrompt> pendingPrompts = new();
    private IServerNetworkChannel? serverChannel;

    public void Register(
        ICoreServerAPI api,
        NetworkClientMessageHandler<RentalConfirmPacket> onConfirm,
        NetworkClientMessageHandler<RentalCancelPacket> onCancel)
    {
        serverChannel = api.Network.RegisterChannel("rentalpermission")
            .RegisterMessageType<RentalPromptPacket>()
            .RegisterMessageType<RentalConfirmPacket>()
            .RegisterMessageType<RentalCancelPacket>()
            .SetMessageHandler<RentalConfirmPacket>(onConfirm)
            .SetMessageHandler<RentalCancelPacket>(onCancel);
    }

    public bool SendPrompt(IServerPlayer player, PendingRentalPrompt prompt, RentalPromptPacket packet)
    {
        if (serverChannel == null)
        {
            return false;
        }

        pendingPrompts[prompt.RequestId] = prompt;
        serverChannel.SendPacket(packet, player);
        return true;
    }

    public void CancelPrompt(IServerPlayer player, string requestId)
    {
        if (pendingPrompts.TryGetValue(requestId, out PendingRentalPrompt? prompt)
            && prompt.PlayerUID == player.PlayerUID)
        {
            pendingPrompts.Remove(requestId);
        }
    }

    public bool TryConsumePrompt(IServerPlayer player, string requestId, out PendingRentalPrompt? prompt)
    {
        prompt = null;
        if (!pendingPrompts.TryGetValue(requestId, out PendingRentalPrompt? candidate)
            || candidate.PlayerUID != player.PlayerUID)
        {
            return false;
        }

        pendingPrompts.Remove(requestId);
        if ((DateTime.UtcNow - candidate.CreatedAtUtc).TotalSeconds > 60)
        {
            return false;
        }

        prompt = candidate;
        return true;
    }
}
