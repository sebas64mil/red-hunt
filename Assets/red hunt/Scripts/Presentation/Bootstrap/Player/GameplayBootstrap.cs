using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;


public class GameplayBootstrap : MonoBehaviour
{
    private PlayerInputHandler playerInputHandler;
    private PlayerMovement playerMovement;
    private PlayerNetworkService playerNetworkService;
    private RemotePlayerSync remotePlayerSync;
    private GameUI gameUI;

    private NetworkBootstrap networkBootstrap;
    private ApplicationBootstrap applicationBootstrap;
    private PresentationBootstrap presentationBootstrap;

    private RemotePlayerMovementManager remotePlayerMovementManager;
    private GameStateManager gameStateManager;

    private int localPlayerId = -1;
    private bool initialized = false;
    private string currentScene = "";

    private HealthUIDisplay healthUIDisplay;

    public void Init(
        NetworkBootstrap network, 
        ApplicationBootstrap application, 
        PresentationBootstrap presentation, 
        GameUI gameUIReference,
        HealthUIDisplay healthUIDisplayReference = null)
    {
        networkBootstrap = network ?? throw new ArgumentNullException(nameof(network));
        applicationBootstrap = application ?? throw new ArgumentNullException(nameof(application));
        presentationBootstrap = presentation ?? throw new ArgumentNullException(nameof(presentation));
        gameUI = gameUIReference;
        healthUIDisplay = healthUIDisplayReference;

        remotePlayerMovementManager = new RemotePlayerMovementManager();
        gameStateManager = FindFirstObjectByType<GameStateManager>();

        if (gameStateManager == null)
        {
            Debug.LogError("[GameplayBootstrap] ❌ GameStateManager no encontrado en escena - WIN CONDITION FALLARÁ");
        }

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

    public void SetGameUI(GameUI gameUIReference)
    {
        gameUI = gameUIReference;
        Debug.Log("[GameplayBootstrap] GameUI asignado via SetGameUI()");
    }

    private void HandlePlayerIdAssigned(int id)
    {
        if (initialized && currentScene != "Game") return;
        
        localPlayerId = id;
        Debug.Log("[GameplayBootstrap] PlayerId asignado: " + localPlayerId);
        
        TryInitializeIfReady();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GameplayBootstrap] Escena cargada: {scene.name}");
        
        currentScene = scene.name;
        
        if (scene.name == "Win")
        {
            Debug.Log("[GameplayBootstrap] Escena Win detectada - desactivando movimiento");
            DisableAllMovement();
            return;
        }
        
        if (scene.name == "Game")
        {
            if (initialized)
            {
                Debug.Log("[GameplayBootstrap] Reiniciando para nueva partida...");
                initialized = false;
                gameUI = null;
            }
            
            // ⭐ Reinicializar GameStateManager para nueva partida
            gameStateManager = FindFirstObjectByType<GameStateManager>();
            if (gameStateManager != null)
            {
                InitializeGameState();
            }
        }
        
        if (scene.name == "Lobby")
        {
            Debug.Log("[GameplayBootstrap] Volviendo al Lobby - limpiando estado de gameplay");
            DisableAllMovement();
            initialized = false;
            gameUI = null;
            
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
            
            return;
        }
        
        if (localPlayerId > 0)
        {
            TryInitializeIfReady();
        }
    }

    private void DisableAllMovement()
    {
        try
        {
            var spawnManager = presentationBootstrap?.Presentation?.SpawnManager;
            if (spawnManager == null) return;

            var allPlayers = applicationBootstrap?.Services?.LobbyManager?.GetAllPlayers();
            if (allPlayers == null) return;

            foreach (var player in allPlayers)
            {
                var playerGO = spawnManager.GetPlayerGameObject(player.Id);
                if (playerGO == null) continue;

                var playerMovement = playerGO.GetComponent<PlayerMovement>();
                var playerInputHandler = playerGO.GetComponent<PlayerInputHandler>();

                if (playerMovement != null)
                    playerMovement.enabled = false;
                if (playerInputHandler != null)
                    playerInputHandler.enabled = false;

                Debug.Log($"[GameplayBootstrap] ✅ Movimiento desactivado para player {player.Id}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameplayBootstrap] Error desactivando movimiento: {e.Message}");
        }
    }

    // ⭐ NUEVO: Inicializar estado de todos los jugadores
    private void InitializeGameState()
    {
        if (gameStateManager == null) return;

        var lobbyManager = applicationBootstrap?.Services?.LobbyManager;
        if (lobbyManager == null) return;

        var allPlayers = lobbyManager.GetAllPlayers();
        foreach (var playerSession in allPlayers)
        {
            // Parsear tipo de jugador
            var playerType = playerSession.PlayerType == PlayerType.Killer.ToString() 
                ? PlayerType.Killer 
                : PlayerType.Escapist;

            // Obtener salud máxima del prefab (por defecto 3 para escapistas)
            int maxHealth = 3;
            if (playerType == PlayerType.Escapist)
            {
                var spawnManager = presentationBootstrap?.Presentation?.SpawnManager;
                var playerGO = spawnManager?.GetPlayerGameObject(playerSession.Id);
                if (playerGO != null)
                {
                    var escapistHealth = playerGO.GetComponent<EscapistHealth>();
                    if (escapistHealth != null)
                    {
                        maxHealth = escapistHealth.MaxHealth;
                    }
                }
            }

            gameStateManager.InitializePlayer(playerSession.Id, maxHealth, playerType);
            Debug.Log($"[GameplayBootstrap] ✅ GameStateManager inicializado para player {playerSession.Id} ({playerType})");
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
        SetupGameUI();
        
        if (gameUI != null)
        {
            var uiBinding = GetComponent<UIBindingBootstrap>();
            if (uiBinding != null)
            {
                uiBinding.SetGameUI(gameUI);
                Debug.Log("[GameplayBootstrap] ✅ GameUI vinculado a UIBindingBootstrap");
            }
        }
        
        SetupLocalPlayerMovement();
        SetupRemotePlayerSyncing();
        Debug.Log("[GameplayBootstrap] Inicializado para PlayerId " + localPlayerId);
    }

    private void SetupGameUI()
    {
        if (gameUI == null)
        {
            gameUI = FindFirstObjectByType<GameUI>();
            if (gameUI == null)
            {
                Debug.LogWarning("[GameplayBootstrap] GameUI no encontrado en escena");
                return;
            }
            Debug.Log("[GameplayBootstrap] ✅ GameUI encontrado en escena Game");
        }

        var isHost = networkBootstrap.Services.ClientState?.IsHost ?? false;
        gameUI.SetIsHost(isHost);
        gameUI.SetConnected(true);
        gameUI.SetLobbyUI(presentationBootstrap?.Presentation?.LobbyUI);

        Debug.Log($"[GameplayBootstrap] ✅ GameUI configurado - IsHost: {isHost}, Connected: true");
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
        
        Debug.Log($"[GameplayBootstrap] ✅ Player encontrado: {playerGO.name} (ID: {localPlayerId})");

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
            
            playerNetworkService.Init(localPlayerId, client, serializer, isHost, broadcastService, spawnManager, lobbyManager);
            Debug.Log("[GameplayBootstrap] ✅ PlayerNetworkService ACTIVADO");
        }

        if (remotePlayerSync != null)
        {
            remotePlayerSync.enabled = false;
            Debug.Log("[GameplayBootstrap] ⭕ RemotePlayerSync DESACTIVADO");
        }

        // ⭐ CRÍTICO: Inicializar KillerAttack con isLocal = true
        var killerAttack = playerGO.GetComponent<KillerAttack>();
        if (killerAttack != null)
        {
            killerAttack.Init(localPlayerId, true);
            Debug.Log("[GameplayBootstrap] ✅ KillerAttack.Init() llamado con isLocal=true");
        }

        // ⭐ Configurar KillerAttack con servicios de red
        SetupKillerAttack(playerGO);

        // ⭐ NUEVO: Inicializar AnimationController
        SetupAnimationController(playerGO);

        // ⭐ Configurar Health UI
        SetupHealthUI(playerGO);

        // ⭐ Configurar Clues Collector
        SetupClueCollector(playerGO);

        // ⭐ Configurar Clues UI
        SetupCluesUI(playerGO);

        Debug.Log("[GameplayBootstrap] ===== LOCAL PLAYER CONFIGURADO =====");
    }

    // ⭐ NUEVO: Configurar Clue Collector (solo para Escapistas)
    private void SetupClueCollector(GameObject playerGO)
    {
        if (playerGO == null) return;

        var clueCollector = playerGO.GetComponent<ClueCollector>();
        if (clueCollector == null)
        {
            Debug.Log("[GameplayBootstrap] ℹ️ ClueCollector no presente (no es Escapist)");
            return;
        }

        try
        {
            var clueRegistry = applicationBootstrap?.Services?.ClueRegistry;
            var broadcastService = networkBootstrap.Services.BroadcastService;
            var client = networkBootstrap.Services.Client;
            var packetBuilder = networkBootstrap.Services.Builder;
            var isHost = networkBootstrap.Services.ClientState?.IsHost ?? false;

            if (clueRegistry == null)
            {
                Debug.LogError("[GameplayBootstrap] ❌ ClueRegistry es NULL");
                return;
            }

            clueCollector.Init(localPlayerId, true, clueRegistry);
            clueCollector.InitNetworkServices(broadcastService, client, packetBuilder, isHost);

            Debug.Log($"[GameplayBootstrap] ✅ ClueCollector using registry: {clueRegistry.GetHashCode()}");
            
            // ⭐ NUEVO: Suscribir ClueCollector a los eventos de red de pistas
            var lobbyNetworkService = networkBootstrap.GetLobbyNetworkService();
            if (lobbyNetworkService != null)
            {
                // Evento 1: Snapshot de todas las pistas - ⭐ CRÍTICO: Sincronizar registro local
                lobbyNetworkService.OnEscapistsCluesSnapshot -= HandleCluesSnapshot;
                lobbyNetworkService.OnEscapistsCluesSnapshot += HandleCluesSnapshot;

                // Evento 2: Pista individual recolectada
                lobbyNetworkService.OnEscapistClueCollected -= HandleClueCollectedFromNetwork;
                lobbyNetworkService.OnEscapistClueCollected += HandleClueCollectedFromNetwork;

                void HandleCluesSnapshot(IReadOnlyDictionary<int, IReadOnlyCollection<string>> cluesByEscapist)
                {
                    // ⭐ NUEVO: mantener targets en el registry que usa la UI
                    clueRegistry.SetTargetEscapists(applicationBootstrap.Services.LobbyManager
                        .GetAllPlayers()
                        .Where(p => p.PlayerType == PlayerType.Escapist.ToString() && p.IsConnected)
                        .Select(p => p.Id));

                    clueRegistry.SyncFromSnapshot(cluesByEscapist);

                    if (cluesByEscapist.TryGetValue(localPlayerId, out var clues))
                    {
                        foreach (var clueId in clues)
                        {
                            clueCollector.OnClueCollectedFromNetwork(clueId);
                        }
                    }
                }

                void HandleClueCollectedFromNetwork(int escapistId, string clueId)
                {
                    Debug.Log($"[GameplayBootstrap] 🔑 Evento individual: Escapist {escapistId} recolectó {clueId}");
                    
                    if (escapistId == localPlayerId)
                    {
                        clueCollector.OnClueCollectedFromNetwork(clueId);
                    }
                }

                Debug.Log("[GameplayBootstrap] ✅ Eventos de clues suscritos correctamente");
            }
            
            Debug.Log("[GameplayBootstrap] ✅ ClueCollector inicializado para Escapist");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameplayBootstrap] ❌ Error en SetupClueCollector: {ex.Message}");
        }
    }

    // ⭐ NUEVO: Configurar Clues UI (solo para Escapistas)
    private void SetupCluesUI(GameObject playerGO)
    {
        if (playerGO == null) return;

        var escapistHealth = playerGO.GetComponent<EscapistHealth>();
        var killerAttack = playerGO.GetComponent<KillerAttack>();
        
        var cluesDisplay = FindFirstObjectByType<EscapistCluesDisplay>();
        if (cluesDisplay == null)
        {
            Debug.LogWarning("[GameplayBootstrap] ⚠️ EscapistCluesDisplay no encontrado en escena");
            return;
        }

        if (killerAttack != null && escapistHealth == null)
        {
            cluesDisplay.HideForKiller();
            Debug.Log("[GameplayBootstrap] 🔪 Player es Killer - Clues UI desactivada");
            return;
        }

        if (escapistHealth != null)
        {
            var clueRegistry = applicationBootstrap?.Services?.ClueRegistry;
            if (clueRegistry != null)
            {
                // ⭐ CRÍTICO: Pasar la MISMA referencia del registry que se está sincronizando
                cluesDisplay.Initialize(clueRegistry, localPlayerId);
                Debug.Log($"[GameplayBootstrap] ✅ EscapistCluesDisplay inicializado para player {localPlayerId}");
                Debug.Log($"[GameplayBootstrap] ✅ EscapistCluesDisplay using registry: {clueRegistry.GetHashCode()}");
                
                // ⭐ NUEVO: Suscribir a snapshots DESPUÉS de inicializar
                var lobbyNetworkService = networkBootstrap.GetLobbyNetworkService();
                if (lobbyNetworkService != null)
                {
                    lobbyNetworkService.OnEscapistsCluesSnapshot -= cluesDisplay.OnSnapshotReceived;
                    lobbyNetworkService.OnEscapistsCluesSnapshot += cluesDisplay.OnSnapshotReceived;
                    Debug.Log("[GameplayBootstrap] ✅ EscapistCluesDisplay suscrito a snapshots");
                }
            }
            return;
        }
    }

    // ⭐ NUEVO: Método para inicializar PlayerAnimationController
    private void SetupAnimationController(GameObject playerGO)
    {
        if (playerGO == null) return;

        var animController = playerGO.GetComponent<PlayerAnimationController>();
        if (animController == null)
        {
            Debug.LogWarning("[GameplayBootstrap] ⚠️ PlayerAnimationController no encontrado, agregando componente...");
            animController = playerGO.AddComponent<PlayerAnimationController>();
        }

        try
        {
            animController.Init(localPlayerId);
            Debug.Log("[GameplayBootstrap] ✅ PlayerAnimationController inicializado");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameplayBootstrap] ❌ Error en SetupAnimationController: {ex.Message}");
        }
    }

    // ⭐ MODIFICADO: Usar .Builder en lugar de .PacketBuilder
    private void SetupKillerAttack(GameObject playerGO)
    {
        if (playerGO == null) return;

        var killerAttack = playerGO.GetComponent<KillerAttack>();
        if (killerAttack == null)
        {
            Debug.Log("[GameplayBootstrap] ℹ️ KillerAttack no presente (no es Killer)");
            return;
        }

        try
        {
            var client = networkBootstrap.Services.Client;
            var broadcastService = networkBootstrap.Services.BroadcastService;
            var packetBuilder = networkBootstrap.Services.Builder;  // ⭐ Usar .Builder
            var isHost = networkBootstrap.Services.ClientState?.IsHost ?? false;

            killerAttack.InitNetworkServices(broadcastService, client, packetBuilder, isHost);
            Debug.Log("[GameplayBootstrap] ✅ KillerAttack con servicios de red inicializado");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameplayBootstrap] ❌ Error en SetupKillerAttack: {ex.Message}");
        }
    }

    private void SetupHealthUI(GameObject playerGO)
    {
        if (playerGO == null) return;

        var escapistHealth = playerGO.GetComponent<EscapistHealth>();
        var killerAttack = playerGO.GetComponent<KillerAttack>();
        
        // Buscar HealthUIDisplay en la escena actual de Game
        var healthUIDisplay = FindFirstObjectByType<HealthUIDisplay>();
        if (healthUIDisplay == null)
        {
            Debug.LogWarning("[GameplayBootstrap] ⚠️ HealthUIDisplay no encontrado en escena Game");
            return;
        }

        // Si es Killer (tiene KillerAttack y NO tiene EscapistHealth), desactivar la UI
        if (killerAttack != null && escapistHealth == null)
        {
            healthUIDisplay.DisableForKiller();
            Debug.Log("[GameplayBootstrap] 🔪 Player es Killer - Health UI desactivada");
            return;
        }

        // Si es Escapist (tiene EscapistHealth), inicializar con su salud
        if (escapistHealth != null)
        {
            // ⭐ Solo pasar un ID (el del jugador local)
            healthUIDisplay.Init(escapistHealth, localPlayerId);
            Debug.Log("[GameplayBootstrap] ✅ HealthUIDisplay configurado para Escapist");
            return;
        }

        Debug.LogWarning("[GameplayBootstrap] ⚠️ Player no es ni Killer ni Escapist - estado desconocido");
    }

    private void SetupRemotePlayerSyncing()
    {
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

        // ⭐ NUEVO: Inicializar AnimationController para jugadores remotos
        var animController = remotePlayerGO.GetComponent<PlayerAnimationController>();
        if (animController == null)
        {
            animController = remotePlayerGO.AddComponent<PlayerAnimationController>();
        }

        try
        {
            animController.Init(remotePlayerId);
            Debug.Log($"[GameplayBootstrap] ✅ PlayerAnimationController inicializado para remoto {remotePlayerId}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameplayBootstrap] ❌ Error inicializando AnimationController para remoto: {ex.Message}");
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