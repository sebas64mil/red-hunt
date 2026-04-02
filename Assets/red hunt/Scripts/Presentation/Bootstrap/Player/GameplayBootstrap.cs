using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameplayBootstrap : MonoBehaviour
{
    private PlayerInputHandler playerInputHandler;
    private PlayerMovement playerMovement;
    private PlayerNetworkService playerNetworkService;
    private RemotePlayerSync remotePlayerSync;

    private NetworkBootstrap networkBootstrap;
    private ApplicationBootstrap applicationBootstrap;
    private PresentationBootstrap presentationBootstrap;

    private RemotePlayerMovementManager remotePlayerMovementManager;

    private int localPlayerId = -1;
    private bool initialized = false;

    public void Init(NetworkBootstrap network, ApplicationBootstrap application, PresentationBootstrap presentation)
    {
        networkBootstrap = network ?? throw new ArgumentNullException(nameof(network));
        applicationBootstrap = application ?? throw new ArgumentNullException(nameof(application));
        presentationBootstrap = presentation ?? throw new ArgumentNullException(nameof(presentation));

        remotePlayerMovementManager = new RemotePlayerMovementManager();

        if (networkBootstrap.Services?.ClientState != null)
        {
            networkBootstrap.Services.ClientState.OnPlayerIdAssigned += HandlePlayerIdAssigned;
            
            localPlayerId = networkBootstrap.Services.ClientState.PlayerId;
            if (localPlayerId > 0)
            {
                Debug.Log("[GameplayBootstrap] PlayerId ya asignado: " + localPlayerId);
            }
            else
            {
                Debug.Log("[GameplayBootstrap] Esperando asignación de PlayerId...");
            }
        }
        else
        {
            Debug.LogError("[GameplayBootstrap] ClientState no disponible");
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void HandlePlayerIdAssigned(int id)
    {
        if (initialized) return;
        
        localPlayerId = id;
        Debug.Log("[GameplayBootstrap] PlayerId asignado: " + localPlayerId);
        
        TryInitializeIfReady();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GameplayBootstrap] Escena cargada: {scene.name}");
        
        if (localPlayerId > 0)
        {
            TryInitializeIfReady();
        }
    }

    private void TryInitializeIfReady()
    {
        if (initialized) return;

        var spawnManager = presentationBootstrap?.Presentation?.SpawnManager;
        if (spawnManager == null || !spawnManager.HasPlayer(localPlayerId))
        {
            Debug.Log("[GameplayBootstrap] Player aún no existe en SpawnManager");
            return;
        }

        initialized = true;
        Debug.Log("[GameplayBootstrap] ===== INICIALIZANDO GAMEPLAY =====");
        SetupLocalPlayerMovement();
        SetupRemotePlayerSyncing();
        Debug.Log("[GameplayBootstrap] Inicializado para PlayerId " + localPlayerId);
    }

    private void SetupLocalPlayerMovement()
    {
        var spawnManager = presentationBootstrap?.Presentation?.SpawnManager;
        var playerGO = spawnManager?.GetPlayerGameObject(localPlayerId);
        
        if (playerGO == null)
        {
            Debug.LogError("[GameplayBootstrap] ❌ Player GameObject NO ENCONTRADO en SpawnManager");
            return;
        }
        
        Debug.Log($"[GameplayBootstrap] ✅ Player encontrado: {playerGO.name}");

        playerInputHandler = playerGO.GetComponent<PlayerInputHandler>();
        playerMovement = playerGO.GetComponent<PlayerMovement>();
        playerNetworkService = playerGO.GetComponent<PlayerNetworkService>();
        remotePlayerSync = playerGO.GetComponent<RemotePlayerSync>();

        Debug.Log($"[GameplayBootstrap] PlayerInputHandler: {(playerInputHandler != null ? "✅ ENCONTRADO" : "❌ NO ENCONTRADO")}");
        Debug.Log($"[GameplayBootstrap] PlayerMovement: {(playerMovement != null ? "✅ ENCONTRADO" : "❌ NO ENCONTRADO")}");
        Debug.Log($"[GameplayBootstrap] PlayerNetworkService: {(playerNetworkService != null ? "✅ ENCONTRADO" : "❌ NO ENCONTRADO")}");

        if (playerInputHandler != null)
        {
            playerInputHandler.enabled = true;
            Debug.Log("[GameplayBootstrap] ✅ PlayerInputHandler ACTIVADO");
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
            playerMovement.SetGravityEnabled(true);
            Debug.Log("[GameplayBootstrap] ✅ PlayerMovement ACTIVADO");
        }

        if (playerNetworkService != null)
        {
            playerNetworkService.enabled = true;
            
            var client = networkBootstrap.Services.Client;
            var broadcastService = networkBootstrap.Services.BroadcastService;
            var serializer = networkBootstrap.Services.Serializer;
            var lobbyManager = applicationBootstrap?.Services?.LobbyManager;
            
            var isHost = networkBootstrap.Services.ClientState?.IsHost ?? false;
            
            Debug.Log($"[GameplayBootstrap] Estado de Client: isConnected={client?.isConnected}, isHost={isHost}");
            
            // ⭐ Pasar spawnManager y lobbyManager (la variable ya existe)
            playerNetworkService.Init(localPlayerId, client, serializer, isHost, broadcastService, spawnManager, lobbyManager);
            Debug.Log("[GameplayBootstrap] ✅ PlayerNetworkService ACTIVADO");
        }

        if (remotePlayerSync != null)
        {
            remotePlayerSync.enabled = false;
            Debug.Log("[GameplayBootstrap] ⭕ RemotePlayerSync DESACTIVADO");
        }

        Debug.Log("[GameplayBootstrap] ===== LOCAL PLAYER CONFIGURADO =====");
    }

    private void SetupRemotePlayerSyncing()
    {
        // ⭐ Registrar todos los jugadores remotos en el manager
        if (applicationBootstrap?.Services?.LobbyManager != null)
        {
            var allPlayers = applicationBootstrap.Services.LobbyManager.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                if (player.Id != localPlayerId)
                {
                    SetupRemotePlayerSync(player.Id);
                    Debug.Log($"[GameplayBootstrap] 🎯 RemotePlayerSync configurado para player {player.Id}");
                }
            }

            applicationBootstrap.OnPlayerJoined += HandleRemotePlayerJoined;
            applicationBootstrap.OnPlayerLeft += HandleRemotePlayerLeft;
        }

        // ⭐ Vincular el callback de red al manager
        if (networkBootstrap?.Services != null)
        {
            networkBootstrap.Services.OnRemotePlayerMoveReceived += remotePlayerMovementManager.ProcessMovePacket;
            Debug.Log("[GameplayBootstrap] ✅ RemotePlayerMovementManager vinculado a OnRemotePlayerMoveReceived");
        }
        else
        {
            Debug.LogError("[GameplayBootstrap] ❌ networkBootstrap.Services es NULL");
        }

        Debug.Log($"[GameplayBootstrap] Remote player syncing configurado ({remotePlayerMovementManager.GetRegisteredRemotePlayersCount()} jugadores)");
    }

    private void SetupRemotePlayerSync(int remotePlayerId)
    {
        var remotePlayerGO = GetRemotePlayerGameObject(remotePlayerId);
        if (remotePlayerGO == null)
        {
            Debug.LogWarning($"[GameplayBootstrap] Remote player {remotePlayerId} GameObject no encontrado");
            return;
        }

        var playerInputHandler = remotePlayerGO.GetComponent<PlayerInputHandler>();
        var playerMovement = remotePlayerGO.GetComponent<PlayerMovement>();
        var playerNetworkService = remotePlayerGO.GetComponent<PlayerNetworkService>();
        var remoteSync = remotePlayerGO.GetComponent<RemotePlayerSync>();

        if (playerInputHandler != null)
        {
            playerInputHandler.enabled = false;
            Debug.Log($"[GameplayBootstrap] PlayerInputHandler DESACTIVADO para remoto {remotePlayerId}");
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
            playerMovement.SetGravityEnabled(true);
            Debug.Log($"[GameplayBootstrap] PlayerMovement DESACTIVADO para remoto {remotePlayerId}");
        }

        if (playerNetworkService != null)
        {
            playerNetworkService.enabled = false;
            Debug.Log($"[GameplayBootstrap] PlayerNetworkService DESACTIVADO para remoto {remotePlayerId}");
        }

        if (remoteSync != null)
        {
            remoteSync.enabled = true;
            remoteSync.Init(remotePlayerId);
            
            remotePlayerMovementManager.RegisterRemotePlayer(remotePlayerId, remoteSync);
            
            Debug.Log($"[GameplayBootstrap] RemotePlayerSync ACTIVADO para remoto {remotePlayerId}");
        }
    }

    private void HandleRemotePlayerJoined(PlayerSession player)
    {
        if (player.Id != localPlayerId)
        {
            SetupRemotePlayerSync(player.Id);
            Debug.Log($"[GameplayBootstrap] 🎯 Nuevo jugador remoto {player.Id} registrado");
        }
    }

    private void HandleRemotePlayerLeft(int playerId)
    {
        remotePlayerMovementManager.UnregisterRemotePlayer(playerId);
        Debug.Log($"[GameplayBootstrap] 🎯 Jugador remoto {playerId} desregistrado");
    }

    private GameObject GetRemotePlayerGameObject(int playerId)
    {
        var spawnManager = presentationBootstrap?.Presentation?.SpawnManager;
        if (spawnManager != null)
        {
            return spawnManager.GetPlayerGameObject(playerId);
        }

        return null;
    }

    private void OnDestroy()
    {
        try
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (networkBootstrap?.Services?.ClientState != null)
            {
                networkBootstrap.Services.ClientState.OnPlayerIdAssigned -= HandlePlayerIdAssigned;
            }

            if (applicationBootstrap != null)
            {
                applicationBootstrap.OnPlayerJoined -= HandleRemotePlayerJoined;
                applicationBootstrap.OnPlayerLeft -= HandleRemotePlayerLeft;
            }

            if (networkBootstrap?.Services != null && remotePlayerMovementManager != null)
            {
                networkBootstrap.Services.OnRemotePlayerMoveReceived -= remotePlayerMovementManager.ProcessMovePacket;
            }

            remotePlayerMovementManager?.Clear();
        }
        catch { }
    }
}