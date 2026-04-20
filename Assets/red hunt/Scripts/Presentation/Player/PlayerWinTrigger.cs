using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerWinTrigger : MonoBehaviour
{
    private int playerId = -1;
    private bool isHost = false;
    private bool isLocal = false;
    private bool gameWon = false;

    private GameNetworkService gameNetworkService;
    private LobbyManager lobbyManager;
    private GameStateManager gameStateManager;

    private readonly HashSet<int> passedEscapists = new();
    private readonly HashSet<int> targetEscapists = new();


    public void Init(int pid, bool hostFlag, bool localFlag, GameNetworkService netService, LobbyManager manager)
    {
        playerId = pid;
        isHost = hostFlag;
        isLocal = localFlag;
        gameNetworkService = netService;
        lobbyManager = manager;

        gameStateManager = FindFirstObjectByType<GameStateManager>();


        if (gameStateManager == null)
        {
            Debug.LogError("[PlayerWinTrigger] CRITICAL: GameStateManager not found in scene at Init");
            return;
        }

        gameStateManager.OnAllEscapistsDead -= HandleAllEscapistsDead;
        gameStateManager.OnAllEscapistsDead += HandleAllEscapistsDead;

        if (gameNetworkService != null)
        {
            gameNetworkService.OnEscapistsPassedSnapshot -= HandleEscapistsPassedSnapshot;
            gameNetworkService.OnEscapistsPassedSnapshot += HandleEscapistsPassedSnapshot;
        }
    }

    private void OnDestroy()
    {
        if (gameStateManager != null)
        {
            gameStateManager.OnAllEscapistsDead -= HandleAllEscapistsDead;
        }

        if (gameNetworkService != null)
        {
            gameNetworkService.OnEscapistsPassedSnapshot -= HandleEscapistsPassedSnapshot;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (gameWon) return;

        if (other.CompareTag("Win"))
        {
            HandleWinCollision();
        }
    }

    private async void HandleWinCollision()
    {
        if (playerId < 0) return;

        if (lobbyManager == null)
        {
            Debug.LogError("[PlayerWinTrigger] LobbyManager is NULL");
            return;
        }

        var playerSession = lobbyManager.GetAllPlayers()?.FirstOrDefault(p => p.Id == playerId);
        if (playerSession == null)
        {
            Debug.LogWarning($"[PlayerWinTrigger] Session not found for player {playerId}");
            return;
        }

        if (playerSession.PlayerType != PlayerType.Escapist.ToString())
        {
            return;
        }

        if (gameNetworkService == null)
        {
            Debug.LogError("[PlayerWinTrigger] GameNetworkService is NULL");
            return;
        }

        try
        {
            if (isHost)
            {
                await gameNetworkService.SendEscapistPassedAsync(playerId);
            }
            else if (isLocal)
            {
                await gameNetworkService.SendEscapistPassedToHostAsync(playerId);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerWinTrigger] Error sending ESCAPIST_PASSED: {e.Message}");
        }
    }

    private async void HandleEscapistsPassedSnapshot(IReadOnlyCollection<int> targetIds, IReadOnlyCollection<int> passedIds)
    {
        if (gameWon) return;

        targetEscapists.Clear();
        passedEscapists.Clear();

        foreach (var id in targetIds)
            targetEscapists.Add(id);

        foreach (var id in passedIds)
            passedEscapists.Add(id);


        if (!isHost) return;
        if (targetEscapists.Count <= 0) return;

        bool allPassed = targetEscapists.All(id => passedEscapists.Contains(id));
        if (!allPassed) return;

        bool allPassedAreAlive = AreAllEscapistsAlive(passedEscapists);
        if (!allPassedAreAlive)
        {
            Debug.LogWarning("[PlayerWinTrigger] Some escapists passed but are dead by killer - ESCAPISTS DO NOT WIN");
            return;
        }


        gameWon = true;

        try
        {
            int winnerId = targetEscapists.Min();
            string winnerType = PlayerType.Escapist.ToString();
            bool isKillerWin = false;

            await gameNetworkService.SendGameWinAsync(winnerId, winnerType, isKillerWin);
            GameManager.ChangeScene("Win");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerWinTrigger] Error sending WIN_GAME (snapshot all passed): {e.Message}");
        }
    }

    private bool AreAllEscapistsAlive(HashSet<int> escapistIds)
    {
        if (gameStateManager == null)
        {
            Debug.LogWarning("[PlayerWinTrigger] GameStateManager is NULL - cannot validate if escapists are alive");
            return false;
        }

        foreach (var escapistId in escapistIds)
        {
            var playerSession = lobbyManager?.GetAllPlayers()?.FirstOrDefault(p => p.Id == escapistId);
            if (playerSession == null)
            {
                Debug.LogWarning($"[PlayerWinTrigger] Session not found for escapist {escapistId}");
                return false;
            }
        }

        int aliveEscapists = gameStateManager.GetAliveEscapistCount();
        int totalTargetEscapists = escapistIds.Count;

        return aliveEscapists >= totalTargetEscapists;
    }

    private async void HandleAllEscapistsDead()
    {

        if (gameWon) return;
        gameWon = true;

        if (lobbyManager == null)
        {
            Debug.LogError("[PlayerWinTrigger] LobbyManager is NULL");
            return;
        }

        var killerSession = lobbyManager.GetAllPlayers()
            ?.FirstOrDefault(p => p.PlayerType == PlayerType.Killer.ToString());

        if (killerSession == null)
        {
            Debug.LogError("[PlayerWinTrigger] Killer not found");
            return;
        }

        int killerId = killerSession.Id;
        string winnerType = PlayerType.Killer.ToString();
        bool isKillerWin = true;

        if (gameNetworkService == null)
        {
            Debug.LogError("[PlayerWinTrigger] GameNetworkService is NULL");
            return;
        }

        try
        {
            if (isHost)
            {
                await gameNetworkService.SendGameWinAsync(killerId, winnerType, isKillerWin);

                GameManager.ChangeScene("Win");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerWinTrigger] Error sending WIN_GAME (Killer): {e.Message}");
        }
    }

    public void Reset()
    {
        gameWon = false;
        passedEscapists.Clear();
        targetEscapists.Clear();
    }
}
