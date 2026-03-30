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
            Debug.LogWarning("[Client] ASSIGN_PLAYER inválido");
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
            // .....................
        }

        Debug.Log($"[Client] Mi ID asignado es: {packet.id}");

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
            Debug.LogWarning("[Client] ASSIGN_REJECT inválido");
            return;
        }

        Debug.LogWarning($"[Client] ASSIGN_REJECT recibido para id:{packet.id} motivo:{packet.reason}");
        try
        {
            state.ClearPendingPlayerType();
            state.SetPlayerId(-1);
            (client as Client)?.Disconnect();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Client] Error al procesar ASSIGN_REJECT: {e.Message}");
        }
    }

    public async Task SendDisconnect()
    {
        Debug.Log("[Client] Sending DISCONNECT");

        var packet = builder.CreateDisconnect();
        await client.SendMessageAsync(packet);
    }
}