using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class LobbyNetworkService : MonoBehaviour
{
    private LobbyManager lobbyManager;
    private BroadcastService broadcastService;
    private PacketBuilder packetBuilder;
    private ClientState clientState;
    private ClientPacketHandler clientPacketHandler;

    private bool isHost;
    public bool IsHost => isHost;

    public SpawnManager SpawnManagerInstance { get; set; }

    public void Init(
        LobbyManager lobbyManager,
        BroadcastService broadcastService,
        PacketBuilder packetBuilder,
        bool isHost,
        ClientState clientState = null,
        ClientPacketHandler clientPacketHandler = null)
    {
        this.lobbyManager = lobbyManager;
        this.broadcastService = broadcastService;
        this.packetBuilder = packetBuilder;
        this.isHost = isHost;
        this.clientState = clientState;
        this.clientPacketHandler = clientPacketHandler;

        lobbyManager.OnPlayerJoined += async (player) => await HandlePlayerJoined(player);
        lobbyManager.OnPlayerReady += async (id) => await HandlePlayerReady(id);

        lobbyManager.OnPlayerLeft += HandlePlayerLeft;
    }

    public void SetIsHost(bool value)
    {
        this.isHost = value;
    }

    public void JoinLobby()
    {
        string playerType = isHost
            ? PlayerType.Killer.ToString()
            : PlayerType.Escapist.ToString();

        if (!isHost)
        {
            clientState?.SetPendingPlayerType(playerType);
            return;
        }

        var command = new JoinLobbyCommand(playerType);
        lobbyManager.ExecuteCommand(command);
    }

    public async Task LeaveLobby()
    {
        Debug.Log("[LobbyNetworkService] Leaving lobby...");

        if (!isHost)
        {
            if (clientPacketHandler != null)
            {
                await clientPacketHandler.SendDisconnect();
                await Task.Delay(100);
            }

            int id = clientState != null ? clientState.PlayerId : -1;
            if (id > 0)
            {
                lobbyManager.RemovePlayerRemote(id);
            }

            return;
        }

        Debug.Log("[LobbyNetworkService] Host leave requested - implementar cierre del host si es necesario");
    }


    private async Task HandlePlayerJoined(PlayerSession player)
    {
        if (!isHost) return;

        Debug.Log("[LobbyNetworkService] Broadcasting PLAYER packets");

        var allPlayers = lobbyManager.GetAllPlayers();
        int hostId = allPlayers.Min(p => p.Id);

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

    private void HandlePlayerLeft(int id)
    {
        Debug.Log($"[LobbyNetworkService] Removing player {id}");

        SpawnManagerInstance?.RemovePlayer(id);
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

            case "REMOVE_PLAYER":
                HandleRemovePlayerPacket(packetJson);
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

        SpawnManagerInstance?.SpawnRemotePlayer(
            playerPacket.id,
            ParsePlayerType(playerPacket.playerType)
        );
    }

    private void HandlePlayerReadyPacket(string json)
    {
        var readyPacket = packetBuilder.Serializer.Deserialize<PlayerReadyPacket>(json);
        if (readyPacket == null) return;

        if (!lobbyManager.GetAllPlayers().Any(p => p.Id == readyPacket.id))
        {
            AddRemotePlayer(readyPacket.id, PlayerType.Escapist.ToString());
        }

        SpawnManagerInstance?.SpawnRemotePlayer(
            readyPacket.id,
            PlayerType.Escapist
        );
    }

    private void HandleAssignPlayerPacket(string json)
    {
        var assignPacket = packetBuilder.Serializer.Deserialize<AssignPlayerPacket>(json);
        if (assignPacket == null) return;

        Debug.Log($"[LobbyNetworkService] Received ASSIGN for id {assignPacket.id}");
    }

    private void HandleRemovePlayerPacket(string json)
    {
        var packet = packetBuilder.Serializer.Deserialize<RemovePlayerPacket>(json);
        if (packet == null) return;

        Debug.Log($"[LobbyNetworkService] Player {packet.id} disconnected");

        lobbyManager.RemovePlayerRemote(packet.id);
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

    private PlayerType ParsePlayerType(string type)
    {
        return type == PlayerType.Killer.ToString()
            ? PlayerType.Killer
            : PlayerType.Escapist;
    }
}