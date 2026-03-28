using System.Net;
using UnityEngine;
public class ConnectionHandler
{
    private readonly ClientConnectionManager connectionManager;
    private readonly IServer server;
    private readonly PacketBuilder builder;
    private readonly LobbyManager lobbyManager;

    public ConnectionHandler(
        ClientConnectionManager manager,
        IServer server,
        PacketBuilder builder,
        LobbyManager lobbyManager)
    {
        this.connectionManager = manager;
        this.server = server;
        this.builder = builder;
        this.lobbyManager = lobbyManager;
    }

    public async void HandleConnect(IPEndPoint sender)
    {
        if (connectionManager.Exists(sender))
            return;

        if (connectionManager.GetClientCount() >= connectionManager.MaxClients)
        {
            Debug.Log("[Connection] Límite de conexiones alcanzado, rechazando conexión");
            return;
        }

        int playerId = connectionManager.AddClient(sender);

        if (playerId <= 0)
        {
            Debug.LogWarning("[Connection] AddClient devolvió id inválido, rechazando conexión");
            return;
        }


        var packet = builder.CreateAssignPlayer(playerId);
        await server.SendToClientAsync(packet, sender);

        Debug.Log($"[Connection] Cliente {sender} asignado ID {playerId}");
    }
}