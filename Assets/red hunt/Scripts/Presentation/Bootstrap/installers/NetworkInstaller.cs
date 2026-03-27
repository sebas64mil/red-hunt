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

        // Crear broadcastService antes de registrar handlers que necesiten reenviar
        var broadcastService = new BroadcastService(server, connectionManager);

        dispatcher.Register("ASSIGN_PLAYER", (json, sender) =>
        {
            clientPacketHandler.HandleAssignPlayer(json);

            // Notificar al LobbyNetworkService en caso de que exista (cliente local)
            lobbyNetworkService?.HandlePacketReceived(json);
        });

        dispatcher.Register("PLAYER", (json, sender) =>
        {
            try
            {
                var packet = serializer.Deserialize<PlayerPacket>(json);
                if (packet == null) return;

                // El servidor registra el jugador y reenvía a todos (broadcast)
                lobbyManager?.AddPlayerRemote(packet.id, packet.playerType);

                // Reenviar a todos los clientes para que se creen las instancias
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

                // Notifica al LobbyNetworkService
                lobbyNetworkService?.HandlePacketReceived(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error procesando PLAYER_READY: {e.Message}");
            }
        });

        Debug.Log("[NetworkInstaller] Network inicializado");

        // Inicializar LobbyNetworkService con sus dependencias (incluyendo clientState)
        lobbyNetworkService?.Init(lobbyManager, broadcastService, builder, isHost, clientState);

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