using System.Net;
using UnityEngine;
using System.Threading.Tasks;

public class ConnectionHandler
{
    private readonly ClientConnectionManager connectionManager;
    private readonly IServer server;
    private readonly PacketBuilder builder;
    private readonly LobbyManager lobbyManager;
    private readonly LobbyNetworkService lobbyNetworkService;

    public ConnectionHandler(
        ClientConnectionManager manager,
        IServer server,
        PacketBuilder builder,
        LobbyManager lobbyManager,
        LobbyNetworkService lobbyNetworkService = null)
    {
        this.connectionManager = manager;
        this.server = server;
        this.builder = builder;
        this.lobbyManager = lobbyManager;
        this.lobbyNetworkService = lobbyNetworkService;
    }

    public async void HandleConnect(IPEndPoint sender)
    {
        try
        {
            if (lobbyNetworkService != null && lobbyNetworkService.IsHost && lobbyNetworkService.GameStarted)
            {
                Debug.LogWarning($"[Connection] Rechazando CONNECT de {sender} porque la partida ya ha comenzado.");

                var rejectJson = builder.CreateAssignReject(-1, "Game already started");
                await server.SendToClientAsync(rejectJson, sender);
                Debug.Log($"[Connection] ASSIGN_REJECT enviado a {sender}");

                try
                {
                    var disconnectJson = builder.CreateDisconnect();
                    await server.SendToClientAsync(disconnectJson, sender);
                    Debug.Log($"[Connection] DISCONNECT dirigido enviado a {sender}");
                }
                catch (System.Exception exDisconnect)
                {
                    Debug.LogWarning($"[Connection] Error enviando DISCONNECT dirigido a {sender}: {exDisconnect.Message}");
                }

                return;
            }

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
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Connection] Error en HandleConnect para {sender}: {e.Message}");
        }
    }
}