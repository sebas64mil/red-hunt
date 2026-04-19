using System;
using UnityEngine;

public static class AdminInstaller
{
    public static AdminNetworkService Install(
        PacketDispatcher dispatcher,
        ISerializer serializer,
        IServer server,
        IClient client,
        PacketBuilder packetBuilder,
        BroadcastService broadcastService,
        ClientConnectionManager connectionManager,
        LobbyManager lobbyManager,
        SpawnManager spawnManager,
        LobbyNetworkService lobbyNetworkService,
        bool isHost,
        ClientState clientState)
    {
        try
        {
            var adminBuilder = new AdminPacketBuilder(serializer);
            var adminService = new AdminNetworkService(server, broadcastService, packetBuilder, adminBuilder, connectionManager, lobbyManager, spawnManager, isHost);
            var adminHandler = new AdminPacketHandler(packetBuilder, adminService, clientState, lobbyManager, client, spawnManager);

            dispatcher.Register("ADMIN_KICK", (json, sender) =>
            {
                try
                {
                    adminHandler.Handle(json, sender);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AdminInstaller] Error procesando ADMIN_KICK: {e.Message}");
                }
            });

            dispatcher.Register("ADMIN_PAUSE", (json, sender) =>
            {
                try
                {
                    adminHandler.Handle(json, sender);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AdminInstaller] Error procesando ADMIN_PAUSE: {e.Message}");
                }
            });

            // Asignar AdminService a GameManager para acceso estático
            GameManager.AdminService = adminService;
            GameManager.IsHost = isHost;

            return adminService;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminInstaller] Install error: {e.Message}");
            return null;
        }
    }
}