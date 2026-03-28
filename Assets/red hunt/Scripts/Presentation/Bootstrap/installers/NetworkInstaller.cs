using System;
using UnityEngine;

public class NetworkInstaller
{
    public NetworkServices Install(
        Server server,
        Client client,
        LobbyManager lobbyManager,
        LobbyNetworkService lobbyNetworkService,
        bool isHost)
    {
        Debug.Log("[NetworkInstaller] Iniciando instalación de Network...");

        // --- Serializador y builder ---
        var serializer = new JsonSerializer();
        var builder = new PacketBuilder(serializer);
        var dispatcher = new PacketDispatcher(serializer);

        // --- Transportes ---
        var transportServer = new UdpTransport();
        var transportClient = new UdpTransport();

        server.Init(transportServer, dispatcher, serializer);
        client.Init(transportClient, dispatcher);

        // --- Connection Manager y Handler ---
        var connectionManager = new ClientConnectionManager();
        var connectionHandler = new ConnectionHandler(connectionManager, server, builder);

        dispatcher.Register("CONNECT", (json, sender) =>
            connectionHandler.HandleConnect(sender));

        var clientState = new ClientState();
        var clientPacketHandler = new ClientPacketHandler(serializer, clientState, client, builder);

        var broadcastService = new BroadcastService(server, connectionManager);

        dispatcher.Register("ASSIGN_PLAYER", (json, sender) =>
        {
            clientPacketHandler.HandleAssignPlayer(json);

            lobbyNetworkService?.HandlePacketReceived(json);
        });

        dispatcher.Register("PLAYER", (json, sender) =>
        {
            try
            {
                var packet = serializer.Deserialize<PlayerPacket>(json);
                if (packet == null) return;

                lobbyManager?.AddPlayerRemote(packet.id, packet.playerType);

                broadcastService?.SendToAll(json).ConfigureAwait(false);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error procesando PLAYER: {e.Message}");
            }
        });

        dispatcher.Register("PLAYER_READY", (json, sender) =>
        {
            try
            {
                var packet = serializer.Deserialize<PlayerReadyPacket>(json);
                if (packet == null) return;

                Debug.Log($"[NetworkInstaller] PLAYER_READY recibido id:{packet.id}");

                lobbyNetworkService?.HandlePacketReceived(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error procesando PLAYER_READY: {e.Message}");
            }
        });

        dispatcher.Register("REMOVE_PLAYER", (json, sender) =>
        {
            try
            {
                Debug.Log("[NetworkInstaller] REMOVE_PLAYER recibido");
                // Delegar al LobbyNetworkService para que elimine el jugador y actualice el spawn/UI
                lobbyNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error procesando REMOVE_PLAYER: {e.Message}");
            }
        });

        dispatcher.Register("DISCONNECT", async (json, sender) =>
        {
            try
            {
                Debug.Log("[NetworkInstaller] DISCONNECT recibido");

                var clientId = connectionManager.GetClientId(sender);

                if (clientId <= 0)
                {
                    Debug.LogWarning("ClientId inválido en DISCONNECT");
                    return;
                }

                connectionManager.RemoveClient(sender);

                lobbyManager.RemovePlayerRemote(clientId);

                var removePacket = builder.CreateRemovePlayer(clientId);
                await broadcastService.SendToAll(removePacket);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error en DISCONNECT: {e.Message}");
            }
        });

        Debug.Log("[NetworkInstaller] Network inicializado");

        // Inicializar LobbyNetworkService con sus dependencias (incluyendo clientState)
        lobbyNetworkService?.Init(lobbyManager, broadcastService, builder, isHost, clientState, clientPacketHandler);

        // --- Retornar servicios ---
        return new NetworkServices
        {
            Server = server,
            Client = client,
            Dispatcher = dispatcher,
            Builder = builder,
            Serializer = serializer,
            ConnectionManager = connectionManager,
            ClientState = clientState,
            BroadcastService = broadcastService
        };
    }
}

public class NetworkServices
{
    public IServer Server { get; set; }
    public IClient Client { get; set; }
    public PacketDispatcher Dispatcher { get; set; }
    public PacketBuilder Builder { get; set; }
    public ISerializer Serializer { get; set; }
    public ClientConnectionManager ConnectionManager { get; set; }
    public ClientState ClientState { get; set; }
    public BroadcastService BroadcastService { get; set; }
}