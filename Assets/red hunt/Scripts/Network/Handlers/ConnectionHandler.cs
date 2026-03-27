using System.Net;

public class ConnectionHandler
{
    private readonly ClientConnectionManager connectionManager;
    private readonly IServer server;
    private readonly PacketBuilder builder;

    public ConnectionHandler(ClientConnectionManager manager, IServer server, PacketBuilder builder)
    {
        this.connectionManager = manager;
        this.server = server;
        this.builder = builder;
    }

    public async void HandleConnect(IPEndPoint sender)
    {
        if (!connectionManager.Exists(sender))
        {
            int playerId = connectionManager.AddClient(sender);

            var packet = builder.CreateAssignPlayer(playerId); 

            await server.SendToClientAsync(packet, sender);

            UnityEngine.Debug.Log($"[Connection] Cliente {sender} asignado ID {playerId}");
        }
    }
}