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

        var readyPacket = builder.CreatePlayerReady(packet.id);

        await client.SendMessageAsync(readyPacket);
    }
}