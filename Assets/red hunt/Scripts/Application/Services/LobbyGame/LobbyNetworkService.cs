using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
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

    public event Action<string> OnStartGameReceived;
    public event Action<int> OnLocalJoinAccepted;
    public event Action OnReturnToLobbyReceived;
    public event Action<int, string, bool> OnGameWinReceived;

    public event Action<IReadOnlyCollection<int>, IReadOnlyCollection<int>> OnEscapistsPassedSnapshot;

    public bool GameStarted { get; private set; } = false;

    public SpawnManager SpawnManagerInstance { get; set; }

    private IServer server;
    private ClientConnectionManager connectionManager;

    private readonly HashSet<int> passedEscapistsHost = new();

    private EscapistClueRegistry cluesRegistry = new();

    public void Init(
        LobbyManager lobbyManager,
        BroadcastService broadcastService,
        PacketBuilder packetBuilder,
        bool isHost,
        ClientState clientState = null,
        ClientPacketHandler clientPacketHandler = null,
        IServer server = null,
        ClientConnectionManager connectionManager = null)
    {
        this.lobbyManager = lobbyManager;
        this.broadcastService = broadcastService;
        this.packetBuilder = packetBuilder;
        this.isHost = isHost;
        this.clientState = clientState;
        this.clientPacketHandler = clientPacketHandler;

        this.server = server;
        this.connectionManager = connectionManager;

        this.clientState = clientState;
        if (this.clientState != null)
        {
            this.clientState.OnPlayerIdAssigned += id =>
            {
                try
                {
                    SpawnManagerInstance?.SetLocalPlayerId(id);
                }
                catch { }

                try
                {
                    ModularLobbyBootstrap.Instance?.GetComponent<PlayerCameraBootstrap>()?.SetLocalPlayerId(id);
                }
                catch { }
            };
        }

        lobbyManager.OnPlayerJoined += async (player) => await HandlePlayerJoined(player);
        lobbyManager.OnPlayerReady += async (id) => await HandlePlayerReady(id);

        lobbyManager.OnPlayerLeft += HandlePlayerLeft;
    }

    public void SetIsHost(bool value)
    {
        this.isHost = value;
    }

    public void JoinLobby(string playerType = null)
    {
        string resolvedType = playerType;

        if (string.IsNullOrEmpty(resolvedType))
        {
            resolvedType = isHost
                ? PlayerType.Killer.ToString()
                : PlayerType.Escapist.ToString();
        }


        if (GameStarted)
        {
            Debug.LogWarning("[LobbyNetworkService] JoinLobby ignored: game has already started");
            return;
        }

        if (!isHost)
        {
            clientState?.SetPendingPlayerType(resolvedType);
            return;
        }

        try
        {
            clientState?.SetPlayerId(1);
            clientState?.SetConnected(true);
            clientState?.SetIsHost(true);
        }
        catch { }

        var added = lobbyManager.AddPlayerRemote(1, resolvedType);
        if (added != null)
        {
            SpawnManagerInstance?.SpawnRemotePlayer(added.Id, ParsePlayerType(added.PlayerType));

            if (clientState != null && clientState.PlayerId == added.Id)
            {
                try
                {
                    OnLocalJoinAccepted?.Invoke(added.Id);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LobbyNetworkService] Error invoking OnLocalJoinAccepted: {e.Message}");
                }
            }

            return;
        }

        var command = new JoinLobbyCommand(resolvedType);
        lobbyManager.ExecuteCommand(command);
    }

    public async Task LeaveLobby()
    {

        if (!isHost)
        {
            if (clientPacketHandler != null)
            {
                await clientPacketHandler.SendDisconnect();
                await Task.Delay(100);
            }

            try
            {
                var players = lobbyManager.GetAllPlayers().ToList();
                foreach (var p in players)
                {
                    lobbyManager.RemovePlayerRemote(p.Id);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LobbyNetworkService] Error cleaning local state in LeaveLobby: {e.Message}");
            }

            return;
        }

        await ShutdownServer();
    }

    public async Task ShutdownServer()
    {

        try
        {
            var packet = packetBuilder.CreateDisconnect();
            await broadcastService.SendToAll(packet);

            await Task.Delay(100);

            server?.Disconnect();
            connectionManager?.Clear();

            var players = lobbyManager.GetAllPlayers().ToList();
            foreach (var p in players)
            {
                lobbyManager.RemovePlayerRemote(p.Id);
                SpawnManagerInstance?.RemovePlayer(p.Id);
            }

        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] Error in ShutdownServer: {e.Message}");
        }
    }

    public async Task StartGame(string sceneName)
    {
        if (!isHost)
        {
            return;
        }

        if (GameStarted)
        {
            Debug.LogWarning("[LobbyNetworkService] StartGame already invoked - ignoring duplicate call");
            return;
        }

        try
        {
            GameStarted = true;

            var startPacket = packetBuilder.CreateStartGame(sceneName);
            await broadcastService.SendToAll(startPacket);

            const int postStartDelayMs = 500;
            await Task.Delay(postStartDelayMs);

            var allPlayers = lobbyManager.GetAllPlayers().ToList();
            foreach (var p in allPlayers)
            {
                string packetJson = packetBuilder.CreatePlayer(p.Id, p.PlayerType.ToString());
                await broadcastService.SendToAll(packetJson);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] Error in StartGame: {e.Message}");
        }
    }

    private async Task HandlePlayerJoined(PlayerSession player)
    {
        if (!isHost) return;


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

        string packetJson = packetBuilder.CreatePlayerReady(playerId);
        await broadcastService.SendToAll(packetJson);
    }

    private void HandlePlayerLeft(int id)
    {

        passedEscapistsHost.Remove(id);

        SpawnManagerInstance?.RemovePlayer(id);

        if (!GameStarted)
        {
            return;
        }

        try
        {
            var gameState = FindFirstObjectByType<GameStateManager>();
            if (gameState != null)
            {
                gameState.RemovePlayer(id);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbyNetworkService] Error updating GameStateManager when player {id} left: {e.Message}");
        }

        if (!isHost)
        {
            return;
        }

        try
        {
            var targetIds = lobbyManager.GetAllPlayers()
                .Where(p => p.PlayerType == PlayerType.Escapist.ToString() && p.IsConnected)
                .Select(p => p.Id)
                .Distinct()
                .OrderBy(pid => pid)
                .ToList();

            var passedIds = passedEscapistsHost
                .Where(pid => targetIds.Contains(pid))
                .Distinct()
                .OrderBy(pid => pid)
                .ToList();

            var snapshotJson = packetBuilder.CreateEscapistsPassedSnapshot(targetIds, passedIds);

            if (broadcastService != null)
            {
                _ = broadcastService.SendToAll(snapshotJson);
            }

            HandleEscapistsPassedSnapshotPacket(snapshotJson);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbyNetworkService] Error resending snapshot of passed escapists after Left: {e.Message}");
        }
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

            case "ASSIGN_REJECT":
                break;

            case "START_GAME":
                HandleStartGamePacket(packetJson);
                break;

            case "RETURN_TO_LOBBY":
                HandleReturnToLobbyPacket();
                break;

            default:
                Debug.LogWarning($"[LobbyNetworkService] Unknown packet received: {packetType}");
                break;
        }
    }

    private void HandleWinGamePacket(string json)
    {
        var packet = packetBuilder.Serializer.Deserialize<WinGamePacket>(json);
        if (packet == null)
        {
            Debug.LogWarning("[LobbyNetworkService] Invalid WIN_GAME packet");
            return;
        }


        if (isHost)
        {
            _ = BroadcastGameWin(packet.winnerId, packet.winnerType, packet.isKillerWin);
        }

        try
        {
            OnGameWinReceived?.Invoke(packet.winnerId, packet.winnerType, packet.isKillerWin);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbyNetworkService] Error invoking OnGameWinReceived: {e.Message}");
        }
    }

    private async Task BroadcastGameWin(int winnerId, string winnerType, bool isKillerWin)
    {
        if (!isHost || broadcastService == null) return;

        try
        {
            var winPacket = packetBuilder.CreateWinGame(winnerId, winnerType, isKillerWin);


            await broadcastService.SendToAll(winPacket);

            await Task.Delay(100);

        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] Error rebroadcasting WIN_GAME: {e.Message}");
        }
    }


    private void HandleEscapistsPassedSnapshotPacket(string json)
    {
        var packet = packetBuilder.DeserializeEscapistsPassedSnapshot(json);
        if (packet == null)
        {
            Debug.LogWarning("[LobbyNetworkService] Invalid ESCAPISTS_PASSED_SNAPSHOT");
            return;
        }

        try
        {
            OnEscapistsPassedSnapshot?.Invoke(packet.targetEscapistIds, packet.passedEscapistIds);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbyNetworkService] Error invoking OnEscapistsPassedSnapshot: {e.Message}");
        }
    }

    private void HandlePlayerPacket(string json)
    {
        var playerPacket = packetBuilder.Serializer.Deserialize<PlayerPacket>(json);
        if (playerPacket == null) return;

        string requestedType = playerPacket.playerType;

        if (isHost && GameStarted)
        {
            var existsHost = lobbyManager.GetAllPlayers().FirstOrDefault(p => p.Id == playerPacket.id);
            if (existsHost == null)
            {
                Debug.LogWarning($"[LobbyNetworkService] Ignoring join attempt from player {playerPacket.id} because game has already started");

                try
                {
                    var rejectPacket = packetBuilder.CreateAssignReject(playerPacket.id, "Game already started");

                    if (connectionManager != null && server != null)
                    {
                        if (connectionManager.TryGetEndpointById(playerPacket.id, out IPEndPoint endpoint) && endpoint != null)
                        {
                            _ = server.SendToClientAsync(rejectPacket, endpoint);

                            try
                            {
                                var disconnectPacket = packetBuilder.CreateDisconnect();
                                _ = server.SendToClientAsync(disconnectPacket, endpoint);
                            }
                            catch (Exception exDisconnect)
                            {
                                Debug.LogWarning($"[LobbyNetworkService] Error sending DISCONNECT to {endpoint}: {exDisconnect.Message}");
                            }

                            try
                            {
                                connectionManager.RemoveClient(endpoint);
                            }
                            catch (Exception exRemove)
                            {
                                Debug.LogWarning($"[LobbyNetworkService] Error removing endpoint from ConnectionManager: {exRemove.Message}");
                            }

                            return;
                        }
                    }

                    _ = broadcastService.SendToAll(rejectPacket);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LobbyNetworkService] Error sending ASSIGN_REJECT: {e.Message}");
                }

                return;
            }
        }

        if (isHost)
        {
            var currentPlayers = lobbyManager.GetAllPlayers().ToList();
            bool anyKiller = currentPlayers.Any(p => p.PlayerType == PlayerType.Killer.ToString());

            int hostId = currentPlayers.Any() ? currentPlayers.Min(p => p.Id) : -1;
            bool hostIsEscapist = false;
            if (hostId != -1)
            {
                var hostPlayer = currentPlayers.FirstOrDefault(p => p.Id == hostId);
                hostIsEscapist = hostPlayer != null && hostPlayer.PlayerType == PlayerType.Escapist.ToString();
            }

            if (hostIsEscapist && !anyKiller)
            {
                if (requestedType != PlayerType.Killer.ToString())
                {
                    requestedType = PlayerType.Killer.ToString();
                }
            }
            else
            {
                if (requestedType == PlayerType.Killer.ToString() && anyKiller)
                {
                    requestedType = PlayerType.Escapist.ToString();
                }
            }
        }

        var existing = lobbyManager.GetAllPlayers().FirstOrDefault(p => p.Id == playerPacket.id);
        if (existing != null)
        {
            if (existing.PlayerType != requestedType)
            {
                var updated = lobbyManager.UpdatePlayerTypeRemote(playerPacket.id, requestedType);
                if (updated)
                {
                    SpawnManagerInstance?.RemovePlayer(playerPacket.id);
                    SpawnManagerInstance?.SpawnRemotePlayer(
                        playerPacket.id,
                        ParsePlayerType(requestedType)
                    );
                }
            }

            return;
        }

        var added = lobbyManager.AddPlayerRemote(playerPacket.id, requestedType);
        if (added == null)
        {
            Debug.LogWarning($"[LobbyNetworkService] Player {playerPacket.id} could not be added (lobby full or error)");

            try
            {
                var rejectPacket = packetBuilder.CreateAssignReject(playerPacket.id, "Lobby full or internal error");

                if (connectionManager != null && server != null)
                {
                    if (connectionManager.TryGetEndpointById(playerPacket.id, out IPEndPoint endpoint) && endpoint != null)
                    {
                        _ = server.SendToClientAsync(rejectPacket, endpoint);
                    }
                }
                else
                {
                    _ = broadcastService.SendToAll(rejectPacket);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyNetworkService] Error sending ASSIGN_REJECT: {e.Message}");
            }

            return;
        }

        if (clientState != null && clientState.PlayerId == added.Id)
        {
            try
            {
                OnLocalJoinAccepted?.Invoke(added.Id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyNetworkService] Error invoking OnLocalJoinAccepted: {e.Message}");
            }
        }
    }

    private void HandlePlayerReadyPacket(string json)
    {
        var readyPacket = packetBuilder.Serializer.Deserialize<PlayerReadyPacket>(json);
        if (readyPacket == null) return;

        var existing = lobbyManager.GetAllPlayers().FirstOrDefault(p => p.Id == readyPacket.id);
        if (existing != null)
        {
            return;
        }

        var added = lobbyManager.AddPlayerRemote(readyPacket.id, PlayerType.Escapist.ToString());
        if (added == null) return;

        SpawnManagerInstance?.SpawnRemotePlayer(
            added.Id,
            ParsePlayerType(added.PlayerType)
        );
    }

    private void HandleAssignPlayerPacket(string json)
    {
        var assignPacket = packetBuilder.Serializer.Deserialize<AssignPlayerPacket>(json);
        if (assignPacket == null) return;

    }

    private void HandleRemovePlayerPacket(string json)
    {
        var packet = packetBuilder.Serializer.Deserialize<RemovePlayerPacket>(json);
        if (packet == null) return;


        lobbyManager.RemovePlayerRemote(packet.id);
    }

    private void HandleStartGamePacket(string json)
    {
        var packet = packetBuilder.Serializer.Deserialize<StartGamePacket>(json);
        if (packet == null)
        {
            Debug.LogWarning("[LobbyNetworkService] START_GAME received but packet invalid");
            return;
        }

        try
        {
            OnStartGameReceived?.Invoke(packet.sceneName);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbyNetworkService] Error invoking OnStartGameReceived: {e.Message}");
        }
    }

    private void HandleReturnToLobbyPacket()
    {
        try
        {
            GameStarted = false;

            try
            {
                var players = lobbyManager.GetAllPlayers().ToList();
                foreach (var p in players)
                {
                    SpawnManagerInstance?.RemovePlayer(p.Id);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyNetworkService] Error cleaning players: {e.Message}");
            }

            OnReturnToLobbyReceived?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] Error in HandleReturnToLobbyPacket: {e.Message}");
        }
    }

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

    public async Task ReturnAllPlayersToLobby()
    {
        if (!isHost)
        {
            Debug.LogWarning("[LobbyNetworkService] ReturnAllPlayersToLobby: only host can use it");
            return;
        }


        try
        {
            var returnPacket = packetBuilder.CreateReturnToLobby();
            await broadcastService.SendToAll(returnPacket);

        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] Error in ReturnAllPlayersToLobby: {e.Message}");
        }
    }

    public void ResetGameStarted()
    {
        GameStarted = false;
    }

    public async Task SendGameWinAsync(int winnerId, string winnerType, bool isKillerWin)
    {
        if (!isHost)
        {
            Debug.LogWarning("[LobbyNetworkService] SendGameWinAsync: Only host can send WIN_GAME to all");
            return;
        }

        if (broadcastService == null)
        {
            Debug.LogError("[LobbyNetworkService] SendGameWinAsync: BroadcastService not available");
            return;
        }

        try
        {
            var winPacket = packetBuilder.CreateWinGame(winnerId, winnerType, isKillerWin);


            await broadcastService.SendToAll(winPacket);

            HandleWinGamePacket(winPacket);

        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] Error in SendGameWinAsync: {e.Message}\n{e.StackTrace}");
        }
    }

    public void ResetGameWin()
    {
        GameStarted = false;
    }

  
}