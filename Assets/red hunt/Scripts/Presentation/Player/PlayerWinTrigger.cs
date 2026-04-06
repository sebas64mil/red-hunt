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

    private void Start()
    {
        Debug.Log($"[PlayerWinTrigger] Inicializado para player {playerId}");
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
        bool isKillerWin = winnerType == PlayerType.Killer.ToString();

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
                Debug.Log("[PlayerWinTrigger] → Host enviando WIN_GAME a TODOS");
                await lobbyNetworkService.SendGameWinAsync(playerId, winnerType, isKillerWin);
                
                // ✅ Después de enviar, cambiar escena
                GameManager.ChangeScene("Win");
            }
            else if (isLocal)
            {
                // Cliente local: enviar al host (el host lo rebroadcasteará)
                Debug.Log("[PlayerWinTrigger] → Cliente local enviando WIN_GAME AL HOST");
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

    public void Init(int pid, bool hostFlag, bool localFlag, LobbyNetworkService netService, LobbyManager manager)
    {
        playerId = pid;
        isHost = hostFlag;
        isLocal = localFlag;
        lobbyNetworkService = netService;
        lobbyManager = manager;

        Debug.Log($"[PlayerWinTrigger] ✅ Inicializado - PlayerId={playerId}, IsHost={isHost}, IsLocal={isLocal}");
    }

    public void Reset()
    {
        gameWon = false;
    }
}