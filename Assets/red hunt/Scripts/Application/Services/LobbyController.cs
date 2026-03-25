using UnityEngine;
using System;
using System.Net;

public class LobbyController
{
    private readonly IServer server;
    private readonly IClient client;
    private readonly ISerializer serializer;
    private readonly PacketBuilder builder;
    private readonly SpawnUI SpawnUI; // Referencia a la capa de presentación

    private int localPlayerId = 0;
    private string localPlayerType = null;

    public LobbyController(
        IServer server,
        IClient client,
        ISerializer serializer,
        PacketBuilder builder,
        SpawnUI SpawnUI)
    {
        this.server = server;
        this.client = client;
        this.serializer = serializer;
        this.builder = builder;
        this.SpawnUI = SpawnUI; // asignamos la UI/Scene

        // Si somos host (server no nulo), suscribimos al evento para instanciar nuevos jugadores localmente
        if (this.server != null)
        {
            this.server.OnConnected += () => SubscribeEvents();

            // Suscribir a OnPlayerJoined si el servidor lo expone
            var srv = this.server as Server;
            if (srv != null)
            {
                srv.OnPlayerJoined += (playerPacket) =>
                {
                    // El host debe instanciar al nuevo jugador en su escena
                    SpawnUI.OnPlayerAssigned(playerPacket.id, playerPacket.playerType);
                };
            }
        }
    }

    // ------------------ Crear Lobby (HOST) ------------------
    public async void CreateLobby(string ip, int port)
    {
        Debug.Log("[Lobby] Creating lobby as HOST...");

        await server.StartServer(port);

        var hostPacket = new PlayerPacket
        {
            type = "ASSIGN_PLAYER",
            id = 1,
            playerType = "KILLER"
        };

        localPlayerId = hostPacket.id;
        localPlayerType = hostPacket.playerType;

        Debug.Log($"[Lobby] HOST registered: {hostPacket.playerType} | ID: {hostPacket.id}");

        // Instanciar al host en escena
        SpawnUI.OnPlayerAssigned(localPlayerId, localPlayerType);
    }

    // ------------------ Unirse al Lobby (CLIENTE) ------------------
    public async void JoinLobby(string ip, int port)
    {
        Debug.Log("[Lobby] Joining lobby...");

        if (client != null)
        {
            client.OnMessageReceived += (msg, ep) =>
            {
                BasePacket basePacket = null;
                try
                {
                    basePacket = serializer.Deserialize<BasePacket>(msg);
                }
                catch
                {
                    Debug.LogWarning("[Lobby] Failed to deserialize base packet");
                    return;
                }

                if (basePacket == null || string.IsNullOrEmpty(basePacket.type)) return;

                switch (basePacket.type)
                {
                    case "ASSIGN_PLAYER":
                        HandleAssignPlayer(msg);
                        break;

                    case "PLAYER_DISCONNECTED":
                        HandlePlayerDisconnected(msg);
                        break;

                    case "PLAYER":
                        HandleOtherPlayer(msg);
                        break;
                }
            };
        }

        await client.ConnectToServer(ip, port);
    }

    // ------------------ Manejo de paquetes ------------------
    private void HandleAssignPlayer(string msg)
    {
        try
        {
            var assign = serializer.Deserialize<PlayerPacket>(msg);
            localPlayerId = assign.id;
            localPlayerType = assign.playerType;

            Debug.Log($"[Lobby] Assigned player ID: {localPlayerId} | Type: {localPlayerType}");

            // Instanciar al jugador en escena
            SpawnUI.OnPlayerAssigned(localPlayerId, localPlayerType);

            SendPlayer();
        }
        catch
        {
            Debug.LogWarning("[Lobby] Failed to deserialize ASSIGN_PLAYER packet");
        }
    }

    private void HandlePlayerDisconnected(string msg)
    {
        try
        {
            var packet = serializer.Deserialize<PlayerPacket>(msg);
            SpawnUI.HandlePlayerDisconnected(packet.id);
        }
        catch
        {
            Debug.LogWarning("[Lobby] Failed to deserialize PLAYER_DISCONNECTED packet");
        }
    }

    private void HandleOtherPlayer(string msg)
    {
        try
        {
            var player = serializer.Deserialize<PlayerPacket>(msg);

            // Evitar instanciar nuestro propio jugador de nuevo
            if (player.id == localPlayerId) return;

            // Instanciar en escena
            SpawnUI.OnPlayerAssigned(player.id, player.playerType);
        }
        catch
        {
            Debug.LogWarning("[Lobby] Failed to deserialize PLAYER packet");
        }
    }

    // ------------------ Enviar info del jugador ------------------
    private async void SendPlayer()
    {
        if (client == null || !client.isConnected)
        {
            Debug.Log("[Lobby] Client not connected, skipping SendPlayer.");
            return;
        }

        var packet = new PlayerPacket
        {
            type = "PLAYER",
            id = localPlayerId,
            playerType = localPlayerType
        };

        string json = serializer.Serialize(packet);
        await client.SendMessageAsync(json);
    }

    // ------------------ Eventos de conexión ------------------
    public void SubscribeEvents()
    {
        if (server != null)
        {
            server.OnConnected += () => Debug.Log("[Server] Client connected");
            server.OnDisconnected += () => Debug.Log("[Server] Disconnected");
            server.OnMessageReceived += (msg, ep) =>
                Debug.Log($"[Server] Received: {msg} from {ep}");
        }

        if (client != null)
        {
            client.OnConnected += () => Debug.Log("[Client] Connected to server");
            client.OnDisconnected += () => Debug.Log("[Client] Disconnected from server");
            client.OnMessageReceived += (msg, ep) => Debug.Log($"[Client] Received: {msg} from {ep}");
        }
    }
}