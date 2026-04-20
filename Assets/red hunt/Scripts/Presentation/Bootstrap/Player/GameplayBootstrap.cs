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

        if (networkBootstrap.Services?.ClientState != null)
        {
            networkBootstrap.Services.ClientState.OnPlayerIdAssigned += HandlePlayerIdAssigned;
            
            localPlayerId = networkBootstrap.Services.ClientState.PlayerId;
        }
        else
        {
            Debug.LogError("[GameplayBootstrap] ClientState is not available");
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public void SetGameUI(GameUI gameUIReference)
    {
        gameUI = gameUIReference;
    }

    private void HandlePlayerIdAssigned(int id)
    {
        if (initialized && currentScene != "Game") return;
        
        localPlayerId = id;
        
        TryInitializeIfReady();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentScene = scene.name;
        
        if (scene.name == "Win")
        {
            DisableAllMovement();
            return;
        }
        
        if (scene.name == "Game")
        {
            if (initialized)
            {
                initialized = false;
                gameUI = null;
            }

            gameStateManager = FindFirstObjectByType<GameStateManager>();
            if (gameStateManager != null)
            {
                gameStateManager.ResetState();
                InitializeGameState();
            }
        }
        
        if (scene.name == "Lobby")
        {
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
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameplayBootstrap] Error disabling movement: {e.Message}");
        }
    }

    private void InitializeGameState()
    {
        if (gameStateManager == null) return;

        var lobbyManager = applicationBootstrap?.Services?.LobbyManager;
        if (lobbyManager == null) return;

        var allPlayers = lobbyManager.GetAllPlayers();
        foreach (var playerSession in allPlayers)
        {
            var playerType = playerSession.PlayerType == PlayerType.Killer.ToString() 
                ? PlayerType.Killer 
                : PlayerType.Escapist;

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
        }
    }

    private void TryInitializeIfReady()
    {
        if (initialized) return;

        var spawnManager = presentationBootstrap?.Presentation?.SpawnManager;
        if (spawnManager == null || !spawnManager.HasPlayer(localPlayerId))
        {
            return;
        }

        initialized = true;
        SetupGameUI();
        
        if (gameUI != null)
        {
            var uiBinding = GetComponent<UIBindingBootstrap>();
            if (uiBinding != null)
            {
                uiBinding.SetGameUI(gameUI);
            }
        }
        
        SetupLocalPlayerMovement();
        SetupRemotePlayerSyncing();

        SetupDoorController();
    }

    private void SetupDoorController()
    {
        try
        {
            var clueRegistry = applicationBootstrap?.Services?.ClueRegistry;
            if (clueRegistry == null)
            {
                Debug.LogError("[GameplayBootstrap] Cannot initialize DoorController: ClueRegistry is null");
                return;
            }

            var door = FindFirstObjectByType<DoorController>();
            if (door == null)
            {
                Debug.LogWarning("[GameplayBootstrap] DoorController not found in scene");
                return;
            }

            door.Init(clueRegistry);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameplayBootstrap] Error in SetupDoorController: {ex.Message}");
        }
    }

    private void SetupGameUI()
    {
        if (gameUI == null)
        {
            gameUI = FindFirstObjectByType<GameUI>();
            if (gameUI == null)
            {
                Debug.LogWarning("[GameplayBootstrap] GameUI not found in scene");
                return;
            }
        }

        var isHost = networkBootstrap.Services.ClientState?.IsHost ?? false;
        GameManager.IsHost = isHost;
        gameUI.SetIsHost(isHost);
        gameUI.SetConnected(true);
        gameUI.SetLobbyUI(presentationBootstrap?.Presentation?.LobbyUI);
    }

    private void SetupLocalPlayerMovement()
    {
        var spawnManager = presentationBootstrap?.Presentation?.SpawnManager;
        var playerGO = spawnManager?.GetPlayerGameObject(localPlayerId);
        
        if (playerGO == null)
        {
            Debug.LogError("[GameplayBootstrap] Player GameObject not found in SpawnManager");
            return;
        }

        playerInputHandler = playerGO.GetComponent<PlayerInputHandler>();
        playerMovement = playerGO.GetComponent<PlayerMovement>();
        playerNetworkService = playerGO.GetComponent<PlayerNetworkService>();
        remotePlayerSync = playerGO.GetComponent<RemotePlayerSync>();

        if (playerInputHandler != null)
        {
            playerInputHandler.enabled = true;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        if (playerNetworkService != null)
        {
            playerNetworkService.enabled = true;
            
            var client = networkBootstrap.Services.Client;
            var broadcastService = networkBootstrap.Services.BroadcastService;
            var serializer = networkBootstrap.Services.Serializer;
            var lobbyManager = applicationBootstrap?.Services?.LobbyManager;
            
            var isHost = networkBootstrap.Services.ClientState?.IsHost ?? false;
            GameManager.IsHost = isHost;
            
            playerNetworkService.Init(localPlayerId, client, serializer, isHost, broadcastService, spawnManager, lobbyManager);
        }

        if (remotePlayerSync != null)
        {
            remotePlayerSync.enabled = false;
        }

        var killerAttack = playerGO.GetComponent<KillerAttack>();
        if (killerAttack != null)
        {
            killerAttack.Init(localPlayerId, true);
        }

        SetupKillerAttack(playerGO);
        SetupAnimationController(playerGO);
        SetupHealthUI(playerGO);
        SetupClueCollector(playerGO);
        SetupCluesUI(playerGO);
    }

    private void SetupClueCollector(GameObject playerGO)
    {
        if (playerGO == null) return;

        var clueCollector = playerGO.GetComponent<ClueCollector>();
        if (clueCollector == null)
        {
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
                Debug.LogError("[GameplayBootstrap] ClueRegistry is null");
                return;
            }

            clueCollector.Init(localPlayerId, true, clueRegistry);
            clueCollector.InitNetworkServices(broadcastService, client, packetBuilder, isHost);
            
            var gameNetworkService = networkBootstrap.GetGameNetworkService();
            if (gameNetworkService != null)
            {
                gameNetworkService.OnEscapistsCluesSnapshot -= HandleCluesSnapshot;
                gameNetworkService.OnEscapistsCluesSnapshot += HandleCluesSnapshot;

                gameNetworkService.OnEscapistClueCollected -= HandleClueCollectedFromNetwork;
                gameNetworkService.OnEscapistClueCollected += HandleClueCollectedFromNetwork;

                void HandleCluesSnapshot(IReadOnlyDictionary<int, IReadOnlyCollection<string>> cluesByEscapist)
                {
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
                    if (escapistId == localPlayerId)
                    {
                        clueCollector.OnClueCollectedFromNetwork(clueId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameplayBootstrap] Error in SetupClueCollector: {ex.Message}");
        }
    }

    private void SetupCluesUI(GameObject playerGO)
    {
        if (playerGO == null) return;

        var escapistHealth = playerGO.GetComponent<EscapistHealth>();
        var killerAttack = playerGO.GetComponent<KillerAttack>();
        
        var cluesDisplay = FindFirstObjectByType<EscapistCluesDisplay>();
        if (cluesDisplay == null)
        {
            Debug.LogWarning("[GameplayBootstrap] EscapistCluesDisplay not found in scene");
            return;
        }

        if (killerAttack != null && escapistHealth == null)
        {
            cluesDisplay.HideForKiller();
            return;
        }

        if (escapistHealth != null)
        {
            var clueRegistry = applicationBootstrap?.Services?.ClueRegistry;
            if (clueRegistry != null)
            {
                cluesDisplay.Initialize(clueRegistry, localPlayerId);
                
                var gameNetworkService = networkBootstrap.GetGameNetworkService();
                if (gameNetworkService != null)
                {
                    gameNetworkService.OnEscapistsCluesSnapshot -= cluesDisplay.OnSnapshotReceived;
                    gameNetworkService.OnEscapistsCluesSnapshot += cluesDisplay.OnSnapshotReceived;
                }
            }
            return;
        }
    }

    private void SetupAnimationController(GameObject playerGO)
    {
        if (playerGO == null) return;

        var animController = playerGO.GetComponent<PlayerAnimationController>();
        if (animController == null)
        {
            animController = playerGO.AddComponent<PlayerAnimationController>();
        }

        try
        {
            animController.Init(localPlayerId);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameplayBootstrap] Error in SetupAnimationController: {ex.Message}");
        }
    }

    private void SetupKillerAttack(GameObject playerGO)
    {
        if (playerGO == null) return;

        var killerAttack = playerGO.GetComponent<KillerAttack>();
        if (killerAttack == null)
        {
            return;
        }

        try
        {
            var client = networkBootstrap.Services.Client;
            var broadcastService = networkBootstrap.Services.BroadcastService;
            var packetBuilder = networkBootstrap.Services.Builder;
            var isHost = networkBootstrap.Services.ClientState?.IsHost ?? false;

            killerAttack.InitNetworkServices(broadcastService, client, packetBuilder, isHost);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameplayBootstrap] Error in SetupKillerAttack: {ex.Message}");
        }
    }

    private void SetupHealthUI(GameObject playerGO)
    {
        if (playerGO == null) return;

        var escapistHealth = playerGO.GetComponent<EscapistHealth>();
        var killerAttack = playerGO.GetComponent<KillerAttack>();
        
        var healthUIDisplay = FindFirstObjectByType<HealthUIDisplay>();
        if (healthUIDisplay == null)
        {
            Debug.LogWarning("[GameplayBootstrap] HealthUIDisplay not found in scene");
            return;
        }

        if (killerAttack != null && escapistHealth == null)
        {
            healthUIDisplay.DisableForKiller();
            return;
        }

        if (escapistHealth != null)
        {
            healthUIDisplay.Init(escapistHealth, localPlayerId);
            return;
        }

        Debug.LogWarning("[GameplayBootstrap] Player is neither Killer nor Escapist - unknown state");
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
                }
            }

            applicationBootstrap.OnPlayerJoined += HandleRemotePlayerJoined;
            applicationBootstrap.OnPlayerLeft += HandleRemotePlayerLeft;
        }

        if (networkBootstrap?.Services != null)
        {
            networkBootstrap.Services.OnRemotePlayerMoveReceived += remotePlayerMovementManager.ProcessMovePacket;
        }
        else
        {
            Debug.LogError("[GameplayBootstrap] networkBootstrap.Services is null");
        }
    }

    private void SetupRemotePlayerSync(int remotePlayerId)
    {
        var remotePlayerGO = GetRemotePlayerGameObject(remotePlayerId);
        if (remotePlayerGO == null)
        {
            Debug.LogWarning($"[GameplayBootstrap] Remote player {remotePlayerId} GameObject not found");
            return;
        }

        var playerInputHandler = remotePlayerGO.GetComponent<PlayerInputHandler>();
        var playerMovement = remotePlayerGO.GetComponent<PlayerMovement>();
        var playerNetworkService = remotePlayerGO.GetComponent<PlayerNetworkService>();
        var remoteSync = remotePlayerGO.GetComponent<RemotePlayerSync>();

        if (playerInputHandler != null)
        {
            playerInputHandler.enabled = false;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (playerNetworkService != null)
        {
            playerNetworkService.enabled = false;
        }

        if (remoteSync != null)
        {
            remoteSync.enabled = true;
            remoteSync.Init(remotePlayerId);
            
            remotePlayerMovementManager.RegisterRemotePlayer(remotePlayerId, remoteSync);
        }

        var animController = remotePlayerGO.GetComponent<PlayerAnimationController>();
        if (animController == null)
        {
            animController = remotePlayerGO.AddComponent<PlayerAnimationController>();
        }

        try
        {
            animController.Init(remotePlayerId);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameplayBootstrap] Error initializing AnimationController for remote player: {ex.Message}");
        }
    }

    private void HandleRemotePlayerJoined(PlayerSession player)
    {
        if (player.Id != localPlayerId)
        {
            SetupRemotePlayerSync(player.Id);
        }
    }

    private void HandleRemotePlayerLeft(int playerId)
    {
        remotePlayerMovementManager.UnregisterRemotePlayer(playerId);
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