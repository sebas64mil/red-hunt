using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class LobbyNetworkService : MonoBehaviour
{
    private LobbyManager lobbyManager;
    private BroadcastService broadcastService;
    private PacketBuilder packetBuilder;
    private ClientState clientState;

    private bool isHost;
    public bool IsHost => isHost;

    public SpawnManager SpawnManagerInstance { get; set; }

    public void Init(LobbyManager lobbyManager, BroadcastService broadcastService, PacketBuilder packetBuilder, bool isHost, ClientState clientState = null)
    {
        this.lobbyManager = lobbyManager;
        this.broadcastService = broadcastService;
        this.packetBuilder = packetBuilder;
        this.isHost = isHost;
        this.clientState = clientState;

        lobbyManager.OnPlayerJoined += async (player) => await HandlePlayerJoined(player);
        lobbyManager.OnPlayerReady += async (id) => await HandlePlayerReady(id);
    }

    public void SetIsHost(bool value)
    {
        this.isHost = value;
    }

    public void JoinLobby()
    {
        string playerType = isHost ? PlayerType.Killer.ToString() : PlayerType.Escapist.ToString();

        if (!isHost)
        {
            // Cliente: NO crear localmente aquí; guardar tipo pendiente para enviarlo al recibir ASSIGN_PLAYER
            clientState?.SetPendingPlayerType(playerType);
            return;
        }

        // Host: crear jugador localmente (host ya tiene autoridad)
        var command = new JoinLobbyCommand(playerType);
        lobbyManager.ExecuteCommand(command);
    }

    // ==================== EVENTOS LOCALES ====================

    private async Task HandlePlayerJoined(PlayerSession player)
    {
        if (!isHost) return;

        Debug.Log("[LobbyNetworkService] Broadcast Lobby State (broadcasting new PLAYER packets)");

        var allPlayers = lobbyManager.GetAllPlayers();
        int hostId = allPlayers.Min(p => p.Id);

        // En lugar de LOBBY_STATE, enviamos un PLAYER por cada jugador (más simple y robusto)
        foreach (var p in allPlayers)
        {
            string packetJson = packetBuilder.CreatePlayer(p.Id, p.PlayerType.ToString());
            await broadcastService.SendToAll(packetJson);
        }
    }

    private async Task HandlePlayerReady(int playerId)
    {
        Debug.Log($"[LobbyNetworkService] Player Ready: {playerId}");

        string packetJson = packetBuilder.CreatePlayerReady(playerId);
        await broadcastService.SendToAll(packetJson);
    }

    // ==================== EVENTOS DE RED ====================

    public void HandlePacketReceived(string packetJson)
    {
        if (string.IsNullOrEmpty(packetJson)) return;

        var packetType = packetBuilder.GetPacketType(packetJson);

        switch (packetType)
        {
            case "PLAYER":
                HandlePlayerPacket(packetJson);
                break;

            case "PLAYER_READY":
                HandlePlayerReadyPacket(packetJson);
                break;

            case "ASSIGN_PLAYER":
                HandleAssignPlayerPacket(packetJson);
                break;

            default:
                Debug.LogWarning($"Paquete desconocido recibido: {packetType}");
                break;
        }
    }

    private void HandlePlayerPacket(string json)
    {
        var playerPacket = packetBuilder.Serializer.Deserialize<PlayerPacket>(json);
        if (playerPacket == null) return;

        if (!lobbyManager.GetAllPlayers().Any(p => p.Id == playerPacket.id))
        {
            AddRemotePlayer(playerPacket.id, playerPacket.playerType);
        }

        SpawnManagerInstance?.SpawnRemotePlayer(playerPacket.id);
    }

    private void HandlePlayerReadyPacket(string json)
    {
        var readyPacket = packetBuilder.Serializer.Deserialize<PlayerReadyPacket>(json);
        if (readyPacket == null) return;

        if (!lobbyManager.GetAllPlayers().Any(p => p.Id == readyPacket.id))
        {
            // Si aún no existe, intentar crear con tipo desconocido (mejor si host ya envió PLAYER)
            AddRemotePlayer(readyPacket.id, PlayerType.Escapist.ToString());
        }

        SpawnManagerInstance?.SpawnRemotePlayer(readyPacket.id);
    }

    private void HandleAssignPlayerPacket(string json)
    {
        var assignPacket = packetBuilder.Serializer.Deserialize<AssignPlayerPacket>(json);
        if (assignPacket == null) return;

        // ASSIGN_PLAYER: normalmente solo contiene ID; el cliente usa este ID para enviar su PLAYER
        // En cliente no hacemos Add aquí; el flujo correcto es que el cliente envíe PLAYER y el servidor lo redistribuya.
        Debug.Log($"[LobbyNetworkService] Received ASSIGN for id {assignPacket.id}");
    }

    // ==================== MÉTODOS DE AYUDA ====================

    public void AddRemotePlayer(int id, string type)
    {
        lobbyManager.AddPlayerRemote(id, type);
    }

    public void AddRemotePlayer(int id, PlayerType type)
    {
        lobbyManager.AddPlayerRemote(id, type.ToString());
    }

    public void SetPlayerReady(int id)
    {
        lobbyManager.SetPlayerReady(id);
    }
}