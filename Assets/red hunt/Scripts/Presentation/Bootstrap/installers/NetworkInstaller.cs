using UnityEngine;

public class NetworkInstaller
{
    public NetworkServices Install(Server server, Client client, LobbyManager lobbyManager)
    {
        Debug.Log("[NetworkInstaller] Iniciando instalación de Network...");

        var serializer = new JsonSerializer();
        var builder = new PacketBuilder(serializer);
        var dispatcher = new PacketDispatcher(serializer);

        var transportServer = new UdpTransport();
        var transportClient = new UdpTransport();

        server.Init(transportServer, dispatcher, serializer);
        client.Init(transportClient, dispatcher);

        var connectionManager = new ClientConnectionManager();
        var connectionHandler = new ConnectionHandler(connectionManager, server, builder);

        dispatcher.Register("CONNECT", (json, sender) =>
            connectionHandler.HandleConnect(sender));

        var clientState = new ClientState();
        var clientPacketHandler = new ClientPacketHandler(serializer, clientState, client, builder);

        dispatcher.Register("ASSIGN_PLAYER", (json, sender) =>
            clientPacketHandler.HandleAssignPlayer(json));

        dispatcher.Register("PLAYER", (json, sender) =>
        {
            try
            {
                var packet = serializer.Deserialize<PlayerPacket>(json);
                if (packet == null) return;

                lobbyManager?.AddPlayerRemote(packet.id, packet.playerType);
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
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error procesando PLAYER_READY: {e.Message}");
            }
        });

        var broadcastService = new BroadcastService(server, connectionManager);

        Debug.Log("[NetworkInstaller] Network inicializado");

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