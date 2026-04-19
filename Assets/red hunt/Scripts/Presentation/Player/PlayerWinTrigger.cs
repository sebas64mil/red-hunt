using System;
using System.Linq;
using UnityEngine;

public class PlayerWinTrigger : MonoBehaviour
{
    private int playerId = -1;
    private bool isHost = false;
    private bool isLocal = false;
    private bool gameWon = false;

    private LobbyNetworkService lobbyNetworkService;
    private LobbyManager lobbyManager;
    private GameStateManager gameStateManager;

    private void Start()
    {
        Debug.Log($"[PlayerWinTrigger] Inicializado para player {playerId}");
    }

    public void Init(int pid, bool hostFlag, bool localFlag, LobbyNetworkService netService, LobbyManager manager)
    {
        playerId = pid;
        isHost = hostFlag;
        isLocal = localFlag;
        lobbyNetworkService = netService;
        lobbyManager = manager;

        gameStateManager = FindFirstObjectByType<GameStateManager>();

        Debug.Log($"[PlayerWinTrigger] ✅ Inicializado - PlayerId={playerId}, IsHost={isHost}, IsLocal={isLocal}");

        if (gameStateManager == null)
        {
            Debug.LogError("[PlayerWinTrigger] ❌ CRÍTICO: GameStateManager no encontrado en escena al Init");
            return;
        }

        // Importante: evitar doble suscripción si Init se llama más de una vez
        gameStateManager.OnAllEscapistsDead -= HandleAllEscapistsDead;
        gameStateManager.OnAllEscapistsDead += HandleAllEscapistsDead;

        Debug.Log("[PlayerWinTrigger] ✅ Suscrito a OnAllEscapistsDead (en Init)");
    }

    private void OnDestroy()
    {
        if (gameStateManager != null)
        {
            gameStateManager.OnAllEscapistsDead -= HandleAllEscapistsDead;
            Debug.Log("[PlayerWinTrigger] ✅ Desuscrito de OnAllEscapistsDead");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (gameWon) return;
        
        // Solo procesar si el tag es "Win"
        if (other.CompareTag("Win"))
        {
            Debug.Log($"[PlayerWinTrigger] Player {playerId} tocó objeto Win: {other.gameObject.name}");
            HandleWinCollision();
        }
    }

    private async void HandleWinCollision()
    {
        if (playerId < 0)
        {
            return;
        }

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

        string winnerType = playerSession.PlayerType;

        // ⭐ CRÍTICO: Solo los escapistas pueden ganar tocando el trigger
        if (winnerType != PlayerType.Escapist.ToString())
        {
            Debug.LogWarning($"[PlayerWinTrigger] ⚠️ Solo los escapistas pueden ganar con el trigger. Player {playerId} es {winnerType}");
            return;
        }

        bool isKillerWin = false;

        Debug.Log($"[PlayerWinTrigger] ✅ {winnerType} (ID: {playerId}) GANÓ - isHost={isHost}, isLocal={isLocal}");

        gameWon = true;

        if (lobbyNetworkService == null)
        {
            Debug.LogError("[PlayerWinTrigger] ❌ LobbyNetworkService es NULL");
            return;
        }

        try
        {
            if (isHost)
            {
                // Host: enviar a TODOS y esperar
                Debug.Log("[PlayerWinTrigger] → Host enviando WIN_GAME a TODOS (Escapista)");
                await lobbyNetworkService.SendGameWinAsync(playerId, winnerType, isKillerWin);
                
                // ✅ Después de enviar, cambiar escena
                GameManager.ChangeScene("Win");
            }
            else if (isLocal)
            {
                // Cliente local: enviar al host (el host lo rebroadcasteará)
                Debug.Log("[PlayerWinTrigger] → Cliente local enviando WIN_GAME AL HOST (Escapista)");
                await lobbyNetworkService.SendGameWinToHostAsync(playerId, winnerType, isKillerWin);
                
                // ✅ El cliente espera a recibir el rebroadcast del host
                Debug.Log("[PlayerWinTrigger] ⏳ Esperando rebroadcast del host...");
            }
            else
            {
                // Jugador remoto tocó el trigger (no debería pasar)
                Debug.LogWarning("[PlayerWinTrigger] ⚠️ Jugador remoto tocó win, ignorando");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerWinTrigger] ❌ Error al enviar WIN_GAME: {e.Message}");
        }
    }

    // ⭐ CRÍTICO: Este método se llama cuando todos los escapistas mueren
    private async void HandleAllEscapistsDead()
    {
        Debug.Log($"[PlayerWinTrigger] 🎉 ¡TODOS LOS ESCAPISTAS HAN MUERTO! KILLER GANA!");

        if (gameWon) return;
        gameWon = true;

        // Encontrar al Killer en el LobbyManager
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

        Debug.Log($"[PlayerWinTrigger] ✅ KILLER (ID: {killerId}) GANA POR MATAR TODOS LOS ESCAPISTAS");

        if (lobbyNetworkService == null)
        {
            Debug.LogError("[PlayerWinTrigger] ❌ LobbyNetworkService es NULL");
            return;
        }

        try
        {
            if (isHost)
            {
                // Host: enviar a TODOS y cambiar escena
                Debug.Log("[PlayerWinTrigger] → Host enviando WIN_GAME (Killer - Todos muertos) a TODOS");
                await lobbyNetworkService.SendGameWinAsync(killerId, winnerType, isKillerWin);
                
                // ✅ Cambiar escena DESPUÉS de enviar
                GameManager.ChangeScene("Win");
            }
            else if (isLocal)
            {
                // Cliente local: solo el host puede procesar esto
                Debug.Log("[PlayerWinTrigger] → Cliente local recibió notificación de todos muertos (esperando al host)");
            }
            else
            {
                // Jugador remoto - nada que hacer
                Debug.Log("[PlayerWinTrigger] → Jugador remoto - nada que hacer");
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
    }
}