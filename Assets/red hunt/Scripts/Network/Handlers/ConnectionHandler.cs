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
                Debug.LogWarning($"[Connection] Rejecting CONNECT from {sender} because game has already started");

                var rejectJson = builder.CreateAssignReject(-1, "Game already started");
                await server.SendToClientAsync(rejectJson, sender);

                try
                {
                    var disconnectJson = builder.CreateDisconnect();
                    await server.SendToClientAsync(disconnectJson, sender);
                }
                catch (System.Exception exDisconnect)
                {
                    Debug.LogWarning($"[Connection] Error sending DISCONNECT to {sender}: {exDisconnect.Message}");
                }

                return;
            }

            if (connectionManager.Exists(sender))
                return;

            if (connectionManager.GetClientCount() >= connectionManager.MaxClients)
            {
                return;
            }

            int playerId = connectionManager.AddClient(sender);

            if (playerId <= 0)
            {
                Debug.LogWarning("[Connection] AddClient returned invalid id, rejecting connection");
                return;
            }

            var packet = builder.CreateAssignPlayer(playerId);
            await server.SendToClientAsync(packet, sender);

        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Connection] Error in HandleConnect for {sender}: {e.Message}");
        }
    }
}