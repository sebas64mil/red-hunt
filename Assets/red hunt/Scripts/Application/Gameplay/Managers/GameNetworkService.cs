using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class GameNetworkService : MonoBehaviour
{
    private LobbyManager lobbyManager;
    private BroadcastService broadcastService;
    private PacketBuilder packetBuilder;
    private ClientState clientState;
    private ClientPacketHandler clientPacketHandler;

    private bool isHost;

    public SpawnManager SpawnManagerInstance { get; set; }

    public event Action<int, string, bool> OnGameWinReceived;
    public event Action<int> OnEscapistPassed;
    public event Action<IReadOnlyCollection<int>, IReadOnlyCollection<int>> OnEscapistsPassedSnapshot;

    public event Action<int, string> OnEscapistClueCollected;
    public event Action<IReadOnlyDictionary<int, IReadOnlyCollection<string>>> OnEscapistsCluesSnapshot;

    private readonly HashSet<int> passedEscapistsHost = new();

    private readonly EscapistClueRegistry cluesRegistry = new();

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
    }

    public void SetIsHost(bool value) => isHost = value;


    public async Task SendGameWinToHostAsync(int winnerId, string winnerType, bool isKillerWin)
    {
        if (isHost)
        {
            Debug.LogWarning("[GameNetworkService] SendGameWinToHostAsync: Only clients should use this");
            return;
        }

        if (clientPacketHandler == null)
        {
            Debug.LogError("[GameNetworkService] SendGameWinToHostAsync: ClientPacketHandler not available");
            return;
        }

        try
        {
            await clientPacketHandler.SendGameWin(winnerId, winnerType, isKillerWin);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameNetworkService] Error sending WIN_GAME to host: {e.Message}");
        }
    }

    public async Task SendGameWinAsync(int winnerId, string winnerType, bool isKillerWin)
    {
        if (!isHost)
        {
            Debug.LogWarning("[GameNetworkService] SendGameWinAsync: Only host can send WIN_GAME to all");
            return;
        }

        if (broadcastService == null)
        {
            Debug.LogError("[GameNetworkService] SendGameWinAsync: BroadcastService not available");
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
            Debug.LogError($"[GameNetworkService] Error in SendGameWinAsync: {e.Message}\n{e.StackTrace}");
        }
    }

    public async Task SendEscapistPassedToHostAsync(int escapistId)
    {
        if (isHost)
        {
            Debug.LogWarning("[GameNetworkService] SendEscapistPassedToHostAsync: Only clients should use this");
            return;
        }

        if (clientPacketHandler == null)
        {
            Debug.LogError("[GameNetworkService] SendEscapistPassedToHostAsync: ClientPacketHandler not available");
            return;
        }

        try
        {
            await clientPacketHandler.SendEscapistPassed(escapistId);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameNetworkService] Error sending ESCAPIST_PASSED to host: {e.Message}");
        }
    }

    public async Task SendEscapistPassedAsync(int escapistId)
    {
        if (!isHost)
        {
            Debug.LogWarning("[GameNetworkService] SendEscapistPassedAsync: Only host can send ESCAPIST_PASSED to all");
            return;
        }

        if (broadcastService == null)
        {
            Debug.LogError("[GameNetworkService] SendEscapistPassedAsync: BroadcastService not available");
            return;
        }

        try
        {
            var passedPacket = packetBuilder.CreateEscapistPassed(escapistId);


            await broadcastService.SendToAll(passedPacket);

            HandleEscapistPassedPacket(passedPacket);

        }
        catch (Exception e)
        {
            Debug.LogError($"[GameNetworkService] Error in SendEscapistPassedAsync: {e.Message}\n{e.StackTrace}");
        }
    }


    public void HandlePacketReceived(string packetJson)
    {
        if (string.IsNullOrEmpty(packetJson))
            return;

        var packetType = packetBuilder.GetPacketType(packetJson);

        switch (packetType)
        {
            case "WIN_GAME":
                HandleWinGamePacket(packetJson);
                break;

            case "HEALTH_UPDATE":
                HandleHealthUpdatePacket(packetJson);
                break;

            case "ESCAPIST_PASSED":
                HandleEscapistPassedPacket(packetJson);
                break;

            case "ESCAPISTS_PASSED_SNAPSHOT":
                HandleEscapistsPassedSnapshotPacket(packetJson);
                break;

            case "ESCAPIST_CLUE_COLLECTED":
                HandleEscapistClueCollectedPacket(packetJson);
                break;

            case "ESCAPISTS_CLUES_SNAPSHOT":
                HandleEscapistasCluesSnapshotPacket(packetJson);
                break;

            default:
                Debug.LogWarning($"[GameNetworkService] Unsupported packet: {packetType}");
                break;
        }
    }

    private void HandleHealthUpdatePacket(string json)
    {
        var packet = packetBuilder.DeserializeHealthUpdate(json);
        if (packet == null)
        {
            Debug.LogWarning("[GameNetworkService] Invalid HEALTH_UPDATE packet");
            return;
        }


        try
        {
            var targetPlayerGO = SpawnManagerInstance?.GetPlayerGameObject(packet.playerId);
            if (targetPlayerGO != null)
            {
                var escapistHealth = targetPlayerGO.GetComponent<EscapistHealth>();
                if (escapistHealth != null)
                {
                    escapistHealth.SetHealth(packet.currentHealth);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameNetworkService] Error updating local health: {e.Message}");
        }
    }

    private void HandleWinGamePacket(string json)
    {
        var packet = packetBuilder.Serializer.Deserialize<WinGamePacket>(json);
        if (packet == null)
        {
            Debug.LogWarning("[GameNetworkService] Invalid WIN_GAME packet");
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
            Debug.LogWarning($"[GameNetworkService] Error invoking OnGameWinReceived: {e.Message}");
        }
    }

    private async Task BroadcastGameWin(int winnerId, string winnerType, bool isKillerWin)
    {
        if (!isHost || broadcastService == null)
            return;

        try
        {
            var winPacket = packetBuilder.CreateWinGame(winnerId, winnerType, isKillerWin);
            await broadcastService.SendToAll(winPacket);
            await Task.Delay(100);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameNetworkService] Error rebroadcasting WIN_GAME: {e.Message}");
        }
    }

    private void HandleEscapistPassedPacket(string json)
    {
        var packet = packetBuilder.DeserializeEscapistPassed(json);
        if (packet == null)
        {
            Debug.LogWarning("[GameNetworkService] Invalid ESCAPIST_PASSED packet");
            return;
        }

        Debug.Log($"[GameNetworkService] ESCAPIST_PASSED received: escapistId={packet.escapistId}");

        if (isHost)
        {
            passedEscapistsHost.Add(packet.escapistId);

            var targetIds = lobbyManager.GetAllPlayers()
                .Where(p => p.PlayerType == PlayerType.Escapist.ToString() && p.IsConnected)
                .Select(p => p.Id)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            var passedIds = passedEscapistsHost
                .Where(id => targetIds.Contains(id))
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            var snapshotJson = packetBuilder.CreateEscapistsPassedSnapshot(targetIds, passedIds);

            if (broadcastService != null)
            {
                _ = broadcastService.SendToAll(snapshotJson);
            }

            HandleEscapistsPassedSnapshotPacket(snapshotJson);
            return;
        }

        try
        {
            OnEscapistPassed?.Invoke(packet.escapistId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameNetworkService] Error invoking OnEscapistPassed: {e.Message}");
        }
    }

    private void HandleEscapistsPassedSnapshotPacket(string json)
    {
        var packet = packetBuilder.DeserializeEscapistsPassedSnapshot(json);
        if (packet == null)
        {
            Debug.LogWarning("[GameNetworkService] Invalid ESCAPISTS_PASSED_SNAPSHOT");
            return;
        }

        try
        {
            OnEscapistsPassedSnapshot?.Invoke(packet.targetEscapistIds, packet.passedEscapistIds);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameNetworkService] Error invoking OnEscapistsPassedSnapshot: {e.Message}");
        }
    }

    private void HandleEscapistClueCollectedPacket(string json)
    {
        var packet = packetBuilder.DeserializeEscapistClueCollected(json);
        if (packet == null)
        {
            Debug.LogWarning("[GameNetworkService] Invalid ESCAPIST_CLUE_COLLECTED packet");
            return;
        }


        DesactivateClueInScene(packet.clueId);

        if (!isHost)
        {
            try
            {
                OnEscapistClueCollected?.Invoke(packet.escapistId, packet.clueId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameNetworkService] Error invoking OnEscapistClueCollected: {e.Message}");
            }

            return;
        }

        cluesRegistry.AddClue(packet.escapistId, packet.clueId);

        var targetIds = lobbyManager.GetAllPlayers()
            .Where(p => p.PlayerType == PlayerType.Escapist.ToString() && p.IsConnected)
            .Select(p => p.Id)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        cluesRegistry.SetTargetEscapists(targetIds);

        var snapshotJson = packetBuilder.CreateEscapistsCluesSnapshot(targetIds, cluesRegistry.GetSnapshot());

        if (broadcastService != null)
        {
            _ = broadcastService.SendToAll(snapshotJson);
        }

        HandleEscapistasCluesSnapshotPacket(snapshotJson);
    }

    private void HandleEscapistasCluesSnapshotPacket(string json)
    {
        var packet = packetBuilder.DeserializeEscapistsCluesSnapshot(json);
        if (packet == null)
        {
            Debug.LogWarning("[GameNetworkService] Invalid ESCAPISTS_CLUES_SNAPSHOT");
            return;
        }

        cluesRegistry.SetTargetEscapists(packet.targetEscapistIds);

        var cluesByEscapist = new Dictionary<int, IReadOnlyCollection<string>>();
        foreach (var entry in packet.entries)
        {
            cluesByEscapist[entry.escapistId] = entry.clueIds.AsReadOnly();
        }

        cluesRegistry.SyncFromSnapshot(cluesByEscapist);

        var globalClueIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in packet.entries)
        {
            foreach (var clueId in entry.clueIds)
            {
                globalClueIds.Add(clueId);
            }
        }

        foreach (var clueId in globalClueIds)
        {
            DesactivateClueInScene(clueId);
        }

        try
        {
            OnEscapistsCluesSnapshot?.Invoke(cluesByEscapist);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameNetworkService] Error invoking OnEscapistsCluesSnapshot: {e.Message}");
        }
    }

    private void DesactivateClueInScene(string clueId)
    {
        var allClues = FindObjectsByType<ClueItemController>(FindObjectsSortMode.None);

        foreach (var clue in allClues)
        {
            if (clue.ClueId == clueId && !clue.IsCollected)
            {
                clue.CollectClue();
                return;
            }
        }
    }
}