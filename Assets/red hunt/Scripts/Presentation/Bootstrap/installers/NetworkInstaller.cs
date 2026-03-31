using System;
using System.Linq;
using System.Net;
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
        var connectionManager = new ClientConnectionManager(3);
        var connectionHandler = new ConnectionHandler(connectionManager, server, builder, lobbyManager);

        dispatcher.Register("CONNECT", (json, sender) =>
            connectionHandler.HandleConnect(sender));

        var clientState = new ClientState();
        var clientPacketHandler = new ClientPacketHandler(serializer, clientState, client, builder);

        var broadcastService = new BroadcastService(server, connectionManager);

        var adminService = AdminInstaller.Install(dispatcher,serializer,server,client,builder,broadcastService,connectionManager,lobbyManager,lobbyNetworkService?.SpawnManagerInstance,lobbyNetworkService,isHost,clientState);

        dispatcher.Register("ASSIGN_PLAYER", (json, sender) =>
        {
            clientPacketHandler.HandleAssignPlayer(json);

            lobbyNetworkService?.HandlePacketReceived(json);
        });


        dispatcher.Register("ASSIGN_REJECT", (json, sender) =>
        {
            try
            {
                clientPacketHandler.HandleAssignReject(json);
                lobbyNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error procesando ASSIGN_REJECT: {e.Message}");
            }
        });


        dispatcher.Register("PLAYER", async (json, sender) =>
        {
            try
            {
                var packet = serializer.Deserialize<PlayerPacket>(json);
                if (packet == null) return;

                if (lobbyNetworkService != null && lobbyNetworkService.IsHost)
                {
                    var allPlayers = lobbyManager?.GetAllPlayers() ?? Enumerable.Empty<PlayerSession>();

                    int existingKillers = allPlayers.Count(p =>
                        p.PlayerType == PlayerType.Killer.ToString() && p.Id != packet.id);

                    if (packet.playerType == PlayerType.Killer.ToString() && existingKillers >= 1)
                    {
                        var rejectJson = builder.CreateAssignReject(packet.id, "Ya existe un Killer en el lobby");
                        try
                        {
                            if (server != null)
                                await server.SendToClientAsync(rejectJson, sender);
                        }
                        catch (Exception sendEx)
                        {
                            Debug.LogWarning($"[NetworkInstaller] Error enviando ASSIGN_REJECT al emisor: {sendEx.Message}");
                        }

                        try
                        {
                            connectionManager?.RemoveClient(sender);
                            Debug.Log($"[NetworkInstaller] ConnectionManager: cliente {sender} removido tras ASSIGN_REJECT (id liberado)");
                        }
                        catch (Exception rmEx)
                        {
                            Debug.LogWarning($"[NetworkInstaller] Error liberando id en ConnectionManager tras ASSIGN_REJECT: {rmEx.Message}");
                        }

                        return;
                    }
                }

                var added = lobbyManager?.AddPlayerRemote(packet.id, packet.playerType);

                string authoritativeJson;
                if (added != null)
                {
                    authoritativeJson = builder.CreatePlayer(packet.id, packet.playerType);
                }
                else
                {
                    authoritativeJson = builder.CreateRemovePlayer(packet.id);
                }
                try
                {
                    if (server != null)
                        await server.SendToClientAsync(authoritativeJson, sender);
                }
                catch (Exception sendEx)
                {
                    Debug.LogWarning($"[NetworkInstaller] Error enviando PACKET directo al emisor: {sendEx.Message}");
                }

                try
                {
                    await broadcastService?.SendToAll(authoritativeJson);
                }
                catch (Exception bcEx)
                {
                    Debug.LogWarning($"[NetworkInstaller] Error en broadcast PLAYER: {bcEx.Message}");
                }
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
                lobbyNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error procesando REMOVE_PLAYER: {e.Message}");
            }
        });

        // NEW: register START_GAME so clients receive the packet and change scene
        dispatcher.Register("START_GAME", (json, sender) =>
        {
            try
            {
                var packet = serializer.Deserialize<StartGamePacket>(json);
                if (packet == null) return;

                Debug.Log($"[NetworkInstaller] START_GAME recibido -> escena: {packet.sceneName}");

                // Forward to LobbyNetworkService so PresentationBootstrap (subscribed to OnStartGameReceived)
                // will trigger the local scene change on clients.
                lobbyNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error procesando START_GAME: {e.Message}");
            }
        });

        if (isHost)
        {
            dispatcher.Register("DISCONNECT", async (json, sender) =>
            {
                try
                {
                    Debug.Log("[NetworkInstaller] DISCONNECT recibido (servidor)");

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
                    Debug.LogError($"[NetworkInstaller] Error en DISCONNECT (servidor): {e.Message}");
                }
            });
        }
        else
        {
            dispatcher.Register("DISCONNECT", (json, sender) =>
            {
                try
                {
                    Debug.Log("[NetworkInstaller] DISCONNECT recibido (cliente) - servidor se está cerrando");

                    var players = lobbyManager.GetAllPlayers().ToList();
                    foreach (var p in players)
                    {
                        lobbyManager.RemovePlayerRemote(p.Id);
                    }

                    client.Disconnect();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkInstaller] Error en DISCONNECT (cliente): {e.Message}");
                }
            });
        }

        Debug.Log("[NetworkInstaller] Network inicializado");

        lobbyNetworkService?.Init(lobbyManager, broadcastService, builder, isHost, clientState, clientPacketHandler, server, connectionManager);

        return new NetworkServices
        {
            Server = server,
            Client = client,
            Dispatcher = dispatcher,
            Builder = builder,
            Serializer = serializer,
            ConnectionManager = connection_manager_fallback(connectionManager),
            ClientState = clientState,
            BroadcastService = broadcastService,
            AdminService = adminService

        };
    }

    // small helper to satisfy style in return (keeps previous variable name)
    private ClientConnectionManager connection_manager_fallback(ClientConnectionManager manager) => manager;
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
    public AdminNetworkService AdminService { get; set; }


    private Action<string, IPEndPoint> CreateDisconnectHandler(bool isHost, LobbyManager lobbyManager)
    {
        if (isHost)
        {
            return async (json, sender) =>
            {
                try
                {
                    Debug.Log("[NetworkServices] DISCONNECT recibido (servidor)");

                    var clientId = ConnectionManager.GetClientId(sender);
                    if (clientId <= 0)
                    {
                        Debug.LogWarning("ClientId inválido en DISCONNECT");
                        return;
                    }

                    ConnectionManager.RemoveClient(sender);
                    lobbyManager.RemovePlayerRemote(clientId);

                    var packet = Builder.CreateRemovePlayer(clientId);
                    await BroadcastService.SendToAll(packet);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkServices] Error en DISCONNECT (servidor): {e.Message}");
                }
            };
        }
        else
        {
            return (json, sender) =>
            {
                try
                {
                    Debug.Log("[NetworkServices] DISCONNECT recibido (cliente) - servidor se está cerrando");

                    var players = lobbyManager.GetAllPlayers().ToList();
                    foreach (var p in players)
                        lobbyManager.RemovePlayerRemote(p.Id);

                    Client?.Disconnect();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkServices] Error en DISCONNECT (cliente): {e.Message}");
                }
            };
        }
    }

    public void SwitchToHost(LobbyManager lobbyManager)
    {
        try
        {
            Dispatcher.Unregister("DISCONNECT");
            Dispatcher.Register("DISCONNECT",
                CreateDisconnectHandler(true, lobbyManager));
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkServices] SwitchToHost error: {e.Message}");
        }
    }

    public void SwitchToClient(LobbyManager lobbyManager)
    {
        try
        {
            Dispatcher.Unregister("DISCONNECT");
            Dispatcher.Register("DISCONNECT",
                CreateDisconnectHandler(false, lobbyManager));
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkServices] SwitchToClient error: {e.Message}");
        }
    }
}