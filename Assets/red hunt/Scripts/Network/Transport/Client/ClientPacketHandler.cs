using UnityEngine;
using System.Threading.Tasks;

public class ClientPacketHandler
{
    private readonly ISerializer serializer;
    private readonly ClientState state;
    private readonly IClient client;
    private readonly PacketBuilder builder;

    public ClientPacketHandler(
        ISerializer serializer,
        ClientState state,
        IClient client,
        PacketBuilder builder)
    {
        this.serializer = serializer;
        this.state = state;
        this.client = client;
        this.builder = builder;
    }

    public async void HandleAssignPlayer(string json)
    {
        var packet = serializer.Deserialize<AssignPlayerPacket>(json);

        if (packet == null)
        {
            Debug.LogWarning("[Client] Invalid ASSIGN_PLAYER");
            return;
        }

        state.SetPlayerId(packet.id);

        try
        {
            state.SetConnected(true);
            (client as Client).isServerConnected = true;
        }
        catch
        {
        }


        var pendingType = state.PendingPlayerType;
        if (!string.IsNullOrEmpty(pendingType))
        {
            var playerPacket = builder.CreatePlayer(packet.id, pendingType);
            await client.SendMessageAsync(playerPacket);
            state.ClearPendingPlayerType();
        }
        else
        {
            var readyPacket = builder.CreatePlayerReady(packet.id);
            await client.SendMessageAsync(readyPacket);
        }
    }

    public void HandleAssignReject(string json)
    {
        var packet = serializer.Deserialize<AssignRejectPacket>(json);

        if (packet == null)
        {
            Debug.LogWarning("[Client] Invalid ASSIGN_REJECT");
            return;
        }

        Debug.LogWarning($"[Client] ASSIGN_REJECT received for id:{packet.id} reason:{packet.reason}");
        try
        {
            state.ClearPendingPlayerType();
            state.SetPlayerId(-1);
            (client as Client)?.Disconnect();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Client] Error processing ASSIGN_REJECT: {e.Message}");
        }
    }

    public async Task SendDisconnect()
    {

        var packet = builder.CreateDisconnect();
        await client.SendMessageAsync(packet);
    }

    public async Task SendGameWin(int winnerId, string winnerType, bool isKillerWin)
    {

        try
        {
            var packet = builder.CreateWinGame(winnerId, winnerType, isKillerWin);
            await client.SendMessageAsync(packet);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ClientPacketHandler] Error sending WIN_GAME: {e.Message}");
        }
    }

    public async Task SendEscapistPassed(int escapistId)
    {

        try
        {
            var packet = builder.CreateEscapistPassed(escapistId);
            await client.SendMessageAsync(packet);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ClientPacketHandler] Error sending ESCAPIST_PASSED: {e.Message}");
        }
    }

    public async Task SendEscapistClueCollected(int escapistId, string clueId)
    {
        if (!client.isConnected)
        {
            Debug.LogError("[ClientPacketHandler] Client not connected");
            return;
        }

        try
        {
            var packet = builder.CreateEscapistClueCollected(escapistId, clueId);
            await client.SendMessageAsync(packet);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ClientPacketHandler] Error sending ESCAPIST_CLUE_COLLECTED: {e.Message}");
        }
    }
}