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

    private void Start()
    {
        Debug.Log($"[PlayerWinTrigger] Inicializado para player {playerId}");
    }

    public void Init(int pid, bool hostFlag, bool localFlag, GameNetworkService netService, LobbyManager manager)
    {
        playerId = pid;
        isHost = hostFlag;
        isLocal = localFlag;
        gameNetworkService = netService;
        lobbyManager = manager;

        gameStateManager = FindFirstObjectByType<GameStateManager>();

        Debug.Log($"[PlayerWinTrigger] ✅ Inicializado - PlayerId={playerId}, IsHost={isHost}, IsLocal={isLocal}");

        if (gameStateManager == null)
        {
            Debug.LogError("[PlayerWinTrigger] ❌ CRÍTICO: GameStateManager no encontrado en escena al Init");
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
            Debug.LogError("[PlayerWinTrigger] ❌ LobbyManager es NULL");
            return;
        }

        var playerSession = lobbyManager.GetAllPlayers()?.FirstOrDefault(p => p.Id == playerId);
        if (playerSession == null)
        {
            Debug.LogWarning($"[PlayerWinTrigger] No se encontrado sesión para player {playerId}");
            return;
        }

        if (playerSession.PlayerType != PlayerType.Escapist.ToString())
        {
            return;
        }

        if (gameNetworkService == null)
        {
            Debug.LogError("[PlayerWinTrigger] ❌ GameNetworkService es NULL");
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
            Debug.LogError($"[PlayerWinTrigger] ❌ Error al enviar ESCAPIST_PASSED: {e.Message}");
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

        Debug.Log($"[PlayerWinTrigger] 📸 Snapshot recibido -> passed={passedEscapists.Count}/{targetEscapists.Count}");

        if (!isHost) return;
        if (targetEscapists.Count <= 0) return;

        bool allPassed = targetEscapists.All(id => passedEscapists.Contains(id));
        if (!allPassed) return;

        bool allPassedAreAlive = AreAllEscapistsAlive(passedEscapists);
        if (!allPassedAreAlive)
        {
            Debug.LogWarning("[PlayerWinTrigger] ⚠️ Algunos escapistas pasaron pero están muertos por el killer - ESCAPISTAS NO GANAN");
            return;
        }

        Debug.Log("[PlayerWinTrigger] 🎉 ¡TODOS LOS ESCAPISTAS CONECTADOS HAN PASADO Y ESTÁN VIVOS! ESCAPISTAS GANAN");

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
            Debug.LogError($"[PlayerWinTrigger] ❌ Error al enviar WIN_GAME (snapshot all passed): {e.Message}");
        }
    }

    private bool AreAllEscapistsAlive(HashSet<int> escapistIds)
    {
        if (gameStateManager == null)
        {
            Debug.LogWarning("[PlayerWinTrigger] ⚠️ GameStateManager es NULL - no se puede validar si los escapistas están vivos");
            return false;
        }

        foreach (var escapistId in escapistIds)
        {
            var playerSession = lobbyManager?.GetAllPlayers()?.FirstOrDefault(p => p.Id == escapistId);
            if (playerSession == null)
            {
                Debug.LogWarning($"[PlayerWinTrigger] ⚠️ No se encontró sesión para escapista {escapistId}");
                return false;
            }
        }

        int aliveEscapists = gameStateManager.GetAliveEscapistCount();
        int totalTargetEscapists = escapistIds.Count;

        return aliveEscapists >= totalTargetEscapists;
    }

    private async void HandleAllEscapistsDead()
    {
        Debug.Log("[PlayerWinTrigger] 🎉 ¡TODOS LOS ESCAPISTAS HAN MUERTO! KILLER GANA!");

        if (gameWon) return;
        gameWon = true;

        if (lobbyManager == null)
        {
            Debug.LogError("[PlayerWinTrigger] ❌ LobbyManager es NULL");
            return;
        }

        var killerSession = lobbyManager.GetAllPlayers()
            ?.FirstOrDefault(p => p.PlayerType == PlayerType.Killer.ToString());

        if (killerSession == null)
        {
            Debug.LogError("[PlayerWinTrigger] ❌ No se encontró al Killer");
            return;
        }

        int killerId = killerSession.Id;
        string winnerType = PlayerType.Killer.ToString();
        bool isKillerWin = true;

        if (gameNetworkService == null)
        {
            Debug.LogError("[PlayerWinTrigger] ❌ GameNetworkService es NULL");
            return;
        }

        try
        {
            if (isHost)
            {
                Debug.Log("[PlayerWinTrigger] → Host enviando WIN_GAME (Killer - Todos muertos) a TODOS");
                await gameNetworkService.SendGameWinAsync(killerId, winnerType, isKillerWin);

                GameManager.ChangeScene("Win");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerWinTrigger] ❌ Error al enviar WIN_GAME (Killer): {e.Message}");
        }
    }

    public void Reset()
    {
        gameWon = false;
        passedEscapists.Clear();
        targetEscapists.Clear();
    }
}
