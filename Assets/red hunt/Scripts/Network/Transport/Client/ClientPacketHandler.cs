using UnityEngine;

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

        Debug.Log($"[Client] Mi ID asignado es: {packet.id}");

        // Si el cliente tenía un tipo pendiente (lo solicitó al hacer Join), enviar PLAYER con tipo + id
        var pendingType = state.PendingPlayerType;
        if (!string.IsNullOrEmpty(pendingType))
        {
            var playerPacket = builder.CreatePlayer(packet.id, pendingType);
            await client.SendMessageAsync(playerPacket);
            state.ClearPendingPlayerType();
        }
        else
        {
            // Como fallback enviar ready (flujo anterior)
            var readyPacket = builder.CreatePlayerReady(packet.id);
            await client.SendMessageAsync(readyPacket);
        }
    }
}