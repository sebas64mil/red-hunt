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

            try
            {
                lobbyNetworkService?.SetAdminService(adminService);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AdminInstaller] No se pudo setear adminService en LobbyNetworkService: {ex.Message}");
            }

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

            return adminService;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminInstaller] Install error: {e.Message}");
            return null;
        }
    }
}