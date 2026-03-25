using UnityEngine;

public class LobbyController
{
    private readonly IServer server;
    private readonly IClient client;
    private readonly ISerializer serializer;
    private readonly PacketBuilder builder;

    public LobbyController(
        IServer server,
        IClient client,
        ISerializer serializer,
        PacketBuilder builder)
    {
        this.server = server;
        this.client = client;
        this.serializer = serializer;
        this.builder = builder;
    }

    public async void CreateLobby(string ip , int port)
    {
        Debug.Log("[Lobby] Creating lobby as HOST...");

        await server.StartServer(port);
        await client.ConnectToServer(ip, port);

        SendPlayer();
    }

    public async void JoinLobby(string ip,int port)
    {
        Debug.Log("[Lobby] Joining lobby...");

        await client.ConnectToServer(ip, port);

        SendPlayer();
    }

    private async void SendPlayer()
    {
        var packet = new PlayerPacket
        {
            type = "PLAYER"
        };

        string json = serializer.Serialize(packet);

        await client.SendMessageAsync(json);
    }
}