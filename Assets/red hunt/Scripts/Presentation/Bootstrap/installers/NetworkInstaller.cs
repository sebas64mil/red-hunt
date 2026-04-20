using System;
using System.Collections.Generic;
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
        GameNetworkService gameNetworkService,
        bool isHost)
    {
        var serializer = new JsonSerializer();
        var builder = new PacketBuilder(serializer);
        var playerPacketBuilder = new PlayerPacketBuilder(serializer);
        var dispatcher = new PacketDispatcher(serializer);

        var transportServer = new UdpTransport();
        var transportClient = new UdpTransport();

        server.Init(transportServer, dispatcher, serializer);
        client.Init(transportClient, dispatcher);

        var connectionManager = new ClientConnectionManager(3);
        var connectionHandler = new ConnectionHandler(connectionManager, server, builder, lobbyManager, lobbyNetworkService);

        dispatcher.Register("CONNECT", (json, sender) =>
            connectionHandler.HandleConnect(sender));

        var clientState = new ClientState();
        var clientPacketHandler = new ClientPacketHandler(serializer, clientState, client, builder);

        var broadcastService = new BroadcastService(server, connectionManager);

        var adminService = AdminInstaller.Install(dispatcher, serializer, server, client, builder, broadcastService, connectionManager, lobbyManager, lobbyNetworkService?.SpawnManagerInstance, lobbyNetworkService, isHost, clientState);

        var networkServices = new NetworkServices();

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
                Debug.LogError($"[NetworkInstaller] Error processing ASSIGN_REJECT: {e.Message}");
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
                        var rejectJson = builder.CreateAssignReject(packet.id, "Killer already exists in lobby");
                        try
                        {
                            if (server != null)
                                await server.SendToClientAsync(rejectJson, sender);
                        }
                        catch (Exception sendEx)
                        {
                            Debug.LogWarning($"[NetworkInstaller] Error sending ASSIGN_REJECT to sender: {sendEx.Message}");
                        }

                        try
                        {
                            connectionManager?.RemoveClient(sender);
                        }
                        catch (Exception rmEx)
                        {
                            Debug.LogWarning($"[NetworkInstaller] Error freeing id in ConnectionManager after ASSIGN_REJECT: {rmEx.Message}");
                        }

                        return;
                    }
                }

                lobbyManager?.AddPlayerRemote(packet.id, packet.playerType);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing PLAYER: {e.Message}");
            }
        });

        dispatcher.Register("PLAYER_READY", (json, sender) =>
        {
            try
            {
                var packet = serializer.Deserialize<PlayerReadyPacket>(json);
                if (packet == null) return;

                lobbyNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing PLAYER_READY: {e.Message}");
            }
        });

        dispatcher.Register("REMOVE_PLAYER", (json, sender) =>
        {
            try
            {
                lobbyNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing REMOVE_PLAYER: {e.Message}");
            }
        });

        dispatcher.Register("MOVE", async (json, sender) =>
        {
            try
            {
                var movePacket = playerPacketBuilder.DeserializeMovePacket(json);
                if (movePacket == null)
                {
                    Debug.LogWarning("[NetworkInstaller] Invalid MOVE packet");
                    return;
                }

                if (isHost)
                {
                    await broadcastService.SendToAllExcept(json, sender);
                }
                else
                {
                    int myPlayerId = clientState.PlayerId;

                    if (movePacket.playerId != myPlayerId)
                    {
                        if (networkServices.OnRemotePlayerMoveReceived != null)
                        {
                            networkServices.OnRemotePlayerMoveReceived.Invoke(movePacket);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing MOVE: {e.Message}\n{e.StackTrace}");
            }
        });

        dispatcher.Register("PLAYER_STATE_SNAPSHOT", (json, sender) =>
        {
            try
            {
                if (isHost)
                {
                    return;
                }

                var snapshot = playerPacketBuilder.DeserializePlayerStateSnapshot(json);
                if (snapshot == null)
                {
                    Debug.LogWarning("[NetworkInstaller] Invalid or null PlayerStateSnapshot");
                    return;
                }

                if (snapshot.players == null || snapshot.players.Count == 0)
                {
                    Debug.LogWarning("[NetworkInstaller] Snapshot with no players");
                    return;
                }

                foreach (var playerState in snapshot.players)
                {
                    if (playerState.playerId != clientState.PlayerId)
                    {
                        var movePacket = new MovePacket
                        {
                            type = "MOVE",
                            playerId = playerState.playerId,
                            posX = playerState.posX,
                            posY = playerState.posY,
                            posZ = playerState.posZ,
                            rotX = playerState.rotX,
                            rotY = playerState.rotY,
                            rotZ = playerState.rotZ,
                            rotW = playerState.rotW,
                            velocityX = playerState.velocityX,
                            velocityY = playerState.velocityY,
                            velocityZ = playerState.velocityZ,
                            isJumping = playerState.isJumping,
                            timestamp = snapshot.timestamp
                        };

                        if (networkServices?.OnRemotePlayerMoveReceived != null)
                        {
                            networkServices.OnRemotePlayerMoveReceived.Invoke(movePacket);
                        }
                        else
                        {
                            Debug.LogError("[NetworkInstaller] OnRemotePlayerMoveReceived is null - GameplayBootstrap not initialized");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing PLAYER_STATE_SNAPSHOT: {e.Message}\n{e.StackTrace}");
            }
        });

        dispatcher.Register("START_GAME", (json, sender) =>
        {
            try
            {
                var packet = serializer.Deserialize<StartGamePacket>(json);
                if (packet == null) return;

                lobbyNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing START_GAME: {e.Message}");
            }
        });

        dispatcher.Register("RETURN_TO_LOBBY", (json, sender) =>
        {
            try
            {
                lobbyNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing RETURN_TO_LOBBY: {e.Message}");
            }
        });

        dispatcher.Register("WIN_GAME", (json, sender) =>
        {
            try
            {
                gameNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing WIN_GAME: {e.Message}");
            }
        });

        dispatcher.Register("HEALTH_UPDATE", async (json, sender) =>
        {
            try
            {
                var packet = serializer.Deserialize<HealthUpdatePacket>(json);
                if (packet == null)
                {
                    Debug.LogWarning("[NetworkInstaller] Invalid HEALTH_UPDATE packet");
                    return;
                }

                if (isHost && broadcastService != null)
                {
                    await broadcastService.SendToAll(json);
                }

                gameNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing HEALTH_UPDATE: {e.Message}");
            }
        });

        dispatcher.Register("ESCAPIST_PASSED", (json, sender) =>
        {
            try
            {
                gameNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing ESCAPIST_PASSED: {e.Message}");
            }
        });

        dispatcher.Register("ESCAPISTS_PASSED_SNAPSHOT", (json, sender) =>
        {
            try
            {
                gameNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing ESCAPISTS_PASSED_SNAPSHOT: {e.Message}");
            }
        });

        dispatcher.Register("ESCAPIST_CLUE_COLLECTED", async (json, sender) =>
        {
            try
            {
                var packet = serializer.Deserialize<EscapistClueCollectedPacket>(json);
                if (packet == null)
                {
                    Debug.LogWarning("[NetworkInstaller] Invalid ESCAPIST_CLUE_COLLECTED packet");
                    return;
                }

                if (isHost && broadcastService != null)
                {
                    await broadcastService.SendToAll(json);
                }

                gameNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing ESCAPIST_CLUE_COLLECTED: {e.Message}");
            }
        });

        dispatcher.Register("ESCAPISTS_CLUES_SNAPSHOT", (json, sender) =>
        {
            try
            {
                gameNetworkService?.HandlePacketReceived(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing ESCAPISTS_CLUES_SNAPSHOT: {e.Message}");
            }
        });

        if (isHost)
        {
            dispatcher.Register("DISCONNECT", async (json, sender) =>
            {
                try
                {
                    var clientId = connectionManager.GetClientId(sender);

                    if (clientId <= 0)
                    {
                        Debug.LogWarning($"[NetworkInstaller] Invalid ClientId ({clientId}) for sender {sender} - client already removed");
                        return;
                    }

                    try
                    {
                        connectionManager.RemoveClient(sender);
                    }
                    catch (KeyNotFoundException ex)
                    {
                        Debug.LogWarning($"[NetworkInstaller] Client {sender} already removed from ConnectionManager: {ex.Message}");
                    }
                    
                    lobbyManager.RemovePlayerRemote(clientId);

                    var removePacket = builder.CreateRemovePlayer(clientId);
                    await broadcastService.SendToAll(removePacket);
                }
                catch (KeyNotFoundException ex)
                {
                    Debug.LogWarning($"[NetworkInstaller] DISCONNECT: Key {sender} not found. Error: {ex.Message}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkInstaller] Error in DISCONNECT (server): {e.Message}");
                }
            });
        }
        else
        {
            dispatcher.Register("DISCONNECT", (json, sender) =>
            {
                try
                {
                    var players = lobbyManager.GetAllPlayers().ToList();
                    foreach (var p in players)
                    {
                        lobbyManager.RemovePlayerRemote(p.Id);
                    }

                    client.Disconnect();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkInstaller] Error in DISCONNECT (client): {e.Message}");
                }
            });
        }

        var adminPacketBuilder = new AdminPacketBuilder(serializer);
        var latencyHandler = new LatencyHandler(connectionManager, serializer);
        var clientPingHandler = new ClientPingHandler(client, serializer, adminPacketBuilder, clientState);

        dispatcher.Register("ADMIN_PONG", (json, sender) =>
        {
            try
            {
                latencyHandler.HandlePong(json, sender);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing ADMIN_PONG: {e.Message}");
            }
        });

        dispatcher.Register("ADMIN_PING", (json, sender) =>
        {
            try
            {
                clientPingHandler.HandlePing(json, sender);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkInstaller] Error processing ADMIN_PING: {e.Message}");
            }
        });

        lobbyNetworkService?.Init(lobbyManager, broadcastService, builder, isHost, clientState, clientPacketHandler, server, connectionManager);
        gameNetworkService?.Init(lobbyManager, broadcastService, builder, isHost, clientState, clientPacketHandler);

        networkServices.Server = server;
        networkServices.Client = client;
        networkServices.Dispatcher = dispatcher;
        networkServices.Builder = builder;
        networkServices.Serializer = serializer;
        networkServices.PlayerPacketBuilder = playerPacketBuilder;
        networkServices.ConnectionManager = connection_manager_fallback(connectionManager);
        networkServices.ClientState = clientState;
        networkServices.BroadcastService = broadcastService;
        networkServices.AdminService = adminService;

        if (isHost)
        {
            var latencyService = new GameObject("[LatencyService]").AddComponent<LatencyService>();
            latencyService.Init(server, adminPacketBuilder, connectionManager);
            networkServices.LatencyService = latencyService;
        }

        return networkServices;
    }

    private ClientConnectionManager connection_manager_fallback(ClientConnectionManager manager) => manager;

    public LatencyService CreateAndInitializeLatencyService(
        IServer server,
        AdminPacketBuilder adminBuilder,
        ClientConnectionManager connectionManager)
    {
        try
        {
            if (server == null)
            {
                Debug.LogError("[NetworkInstaller] IServer is null, cannot create LatencyService");
                return null;
            }

            var latencyService = new GameObject("[LatencyService]").AddComponent<LatencyService>();
            latencyService.Init(server, adminBuilder, connectionManager);

            return latencyService;
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkInstaller] Error creating LatencyService on-demand: {e.Message}");
            return null;
        }
    }
}

public class NetworkServices
{
    public IServer Server { get; set; }
    public IClient Client { get; set; }
    public PacketDispatcher Dispatcher { get; set; }
    public PacketBuilder Builder { get; set; }
    public PlayerPacketBuilder PlayerPacketBuilder { get; set; }
    public ISerializer Serializer { get; set; }
    public ClientConnectionManager ConnectionManager { get; set; }
    public ClientState ClientState { get; set; }
    public BroadcastService BroadcastService { get; set; }
    public AdminNetworkService AdminService { get; set; }
    public LatencyService LatencyService { get; set; }

    public Action<MovePacket> OnRemotePlayerMoveReceived { get; set; }

    private Action<string, IPEndPoint> CreateDisconnectHandler(bool isHost, LobbyManager lobbyManager)
    {
        if (isHost)
        {
            return async (json, sender) =>
            {
                try
                {
                    var clientId = ConnectionManager.GetClientId(sender);
                    
                    if (clientId <= 0)
                    {
                        Debug.LogWarning($"[NetworkServices] Invalid ClientId ({clientId}) for sender {sender} - client already removed");
                        return;
                    }

                    try
                    {
                        ConnectionManager.RemoveClient(sender);
                    }
                    catch (KeyNotFoundException ex)
                    {
                        Debug.LogWarning($"[NetworkServices] Client {sender} already removed from ConnectionManager: {ex.Message}");
                    }
                    
                    lobbyManager.RemovePlayerRemote(clientId);

                    var packet = Builder.CreateRemovePlayer(clientId);
                    await BroadcastService.SendToAll(packet);
                }
                catch (KeyNotFoundException ex)
                {
                    Debug.LogWarning($"[NetworkServices] DISCONNECT: Key {sender} not found. Error: {ex.Message}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkServices] Error in DISCONNECT (server): {e.Message}");
                }
            };
        }
        else
        {
            return (json, sender) =>
            {
                try
                {
                    var players = lobbyManager.GetAllPlayers().ToList();
                    foreach (var p in players)
                        lobbyManager.RemovePlayerRemote(p.Id);

                    Client?.Disconnect();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkServices] Error in DISCONNECT (client): {e.Message}");
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