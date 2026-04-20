using System;
using System.Linq;
using UnityEngine;

public class PresentationBootstrap : MonoBehaviour
{
    public PresentationServices Presentation { get; private set; }

    private LobbyUI queuedLobbyUI;
    private SpawnUI queuedSpawnUI;
    private bool installed = false;

    private ApplicationBootstrap appBootstrap;
    private NetworkBootstrap networkBootstrap;
    private LobbyNetworkService lobbyNetworkService;
    private GameNetworkService gameNetworkService;
    private UIBindingBootstrap uiBindingBootstrap;

    private AdminUI adminUI;

    private Func<bool> shouldSuppressShowForLocal;

    private bool isReturningFromGameScene = false;
    
    private GameStateManager gameStateManager;

    public void Init(LobbyUI lobbyUI = null, SpawnUI spawnUI = null)
    {
        if (lobbyUI != null) queuedLobbyUI = lobbyUI;
        if (spawnUI != null) queuedSpawnUI = spawnUI;
        TryInstall();
    }

    public void AttachUIBinding(UIBindingBootstrap uiBinding)
    {
        uiBindingBootstrap = uiBinding ?? throw new ArgumentNullException(nameof(uiBinding));
    }

    public void RegisterSpawnUI(SpawnUI ui)
    {
        if (ui == null)
        {
            queuedSpawnUI = null;
            if (Presentation != null)
                Presentation.SpawnUI = null;
            return;
        }

        queuedSpawnUI = ui;

        if (!installed)
        {
            TryInstall();
            return;
        }

        try
        {
            int localPlayerId = network_bootstrap_clientstate_id_fallback();

            var killerSpawnParent = ui.GetKillerSpawnParent();
            var escapistSpawnParent = ui.GetEscapistSpawnParent();
            var killerPrefab = ui.KillerPrefab;
            var escapistPrefab = ui.EscapistPrefab;
            var killerSpawnPos = ui.KillerSpawnPosition;
            var escapistBasePos = ui.EscapistBasePosition;
            var escapistSpacing = ui.EscapistSpacing;
            var killerRotationY = ui.KillerRotationY;        
            var escapistRotationY = ui.EscapistRotationY;    

            var newSpawnManager = new SpawnManager(
                killerSpawnParent,
                escapistSpawnParent,
                killerPrefab,
                escapistPrefab,
                localPlayerId,
                killerSpawnPos,
                escapistBasePos,
                escapistSpacing,
                killerRotationY,       
                escapistRotationY   
            );

            Presentation.SpawnManager = newSpawnManager;
            Presentation.SpawnUI = ui;
            Presentation.SpawnUI.Init(newSpawnManager);

            if (lobbyNetworkService != null)
                lobby_network_service_assign(newSpawnManager);

            if (gameNetworkService != null)
                game_network_service_assign(newSpawnManager);

            try
            {
                var players = appBootstrap?.Services?.LobbyManager?.GetAllPlayers();
                if (players != null)
                {
                    foreach (var p in players)
                    {
                        try
                        {
                            if (!Presentation.SpawnManager.HasPlayer(p.Id))
                            {
                                Presentation.SpawnUI.OnPlayerAssigned(p.Id, p.PlayerType);
                            }
                        }
                        catch (Exception exSpawn)
                        {
                            Debug.LogWarning($"[PresentationBootstrap] Error repopulating player {p.Id}: {exSpawn.Message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PresentationBootstrap] Error repopulating spawns: {e.Message}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error re-registering SpawnUI: {e.Message}");
        }
    }

    private void lobby_network_service_assign(SpawnManager manager)
    {
        try
        {
            lobbyNetworkService.SpawnManagerInstance = manager;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Could not assign SpawnManager to LobbyNetworkService: {e.Message}");
        }
    }

    private void game_network_service_assign(SpawnManager manager)
    {
        try
        {
            gameNetworkService.SpawnManagerInstance = manager;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Could not assign SpawnManager to GameNetworkService: {e.Message}");
        }
    }

    public void RegisterLobbyUI(LobbyUI ui)
    {
        if (ui == null)
        {
            queuedLobbyUI = null;
            if (Presentation != null)
                Presentation.LobbyUI = null;
            return;
        }

        queuedLobbyUI = ui;

        if (!installed)
        {
            TryInstall();
            return;
        }

        try
        {
            Presentation.LobbyUI = ui;

            var playerId = networkBootstrap?.Services?.ClientState?.PlayerId ?? -1;
            Presentation.LobbyUI.SetLocalPlayerId(playerId);
            Presentation.LobbyUI.ResetReadyState();
            
            var isHost = networkBootstrap?.Services?.ClientState?.IsHost ?? false;
            Presentation.LobbyUI.SetIsHost(isHost);
            Presentation.LobbyUI.SetConnected(true);
            
            if (!isReturningFromGameScene)
            {
                Presentation.LobbyUI.ShowLobbyPanel();
            }
            else
            {
                Presentation.LobbyUI.ResetAllToMain();
                isReturningFromGameScene = false;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error re-registering LobbyUI: {e.Message}");
        }
    }

    public void SetReturningFromGameScene(bool value)
    {
        isReturningFromGameScene = value;
    }

    public void RegisterAdminUI(AdminUI admin)
    {
        adminUI = admin;
    }

    public void AttachApplication(ApplicationBootstrap app)
    {
        appBootstrap = app ?? throw new ArgumentNullException(nameof(app));
        appBootstrap.OnPlayerJoined += HandlePlayerJoined;
        appBootstrap.OnPlayerLeft += HandlePlayerLeft;
    }

    public void AttachNetwork(NetworkBootstrap net)
    {
        networkBootstrap = net ?? throw new ArgumentNullException(nameof(net));
        lobbyNetworkService = net.GetLobbyNetworkService();
        gameNetworkService = net.GetGameNetworkService();

        networkBootstrap.OnPlayerIdAssigned += HandlePlayerIdAssigned;
        networkBootstrap.OnLocalJoinAccepted += HandleLocalJoinAccepted;
        networkBootstrap.OnClientDisconnected += HandleClientDisconnected;

        if (lobbyNetworkService != null)
        {
            lobbyNetworkService.OnStartGameReceived += HandleStartGameReceived;
            lobbyNetworkService.OnReturnToLobbyReceived += HandleReturnToLobby;
        }

        if (installed && Presentation?.SpawnManager != null)
        {
            if (lobbyNetworkService != null)
                lobbyNetworkService.SpawnManagerInstance = Presentation.SpawnManager;

            if (gameNetworkService != null)
                gameNetworkService.SpawnManagerInstance = Presentation.SpawnManager;
        }
    }

    private void HandleStartGameReceived(string sceneName)
    {
        try
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                if (uiBindingBootstrap != null)
                {
                    uiBindingBootstrap.HandleExternalStartRequest(sceneName);
                }
                else
                {
                    Debug.LogWarning("[PresentationBootstrap] UIBindingBootstrap not assigned, changing scene without disabling cursor");
                    GameManager.ChangeScene(sceneName);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error in HandleStartGameReceived: {e.Message}");
        }
    }

    private void HandleReturnToLobby()
    {
        try
        {
            GameManager.ChangeScene("Lobby");
            GameManager.SetCursorVisible(true);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error in HandleReturnToLobby: {e.Message}");
        }
    }

    public void RegisterShowSuppressionPredicate(Func<bool> predicate)
    {
        shouldSuppressShowForLocal = predicate;
    }

    public GameStateManager GetOrCreateGameStateManager()
    {
        if (gameStateManager == null)
        {
            gameStateManager = FindFirstObjectByType<GameStateManager>();
            
            if (gameStateManager == null)
            {
                var gsManagerGO = new GameObject("[GameStateManager]");
                gameStateManager = gsManagerGO.AddComponent<GameStateManager>();
            }
        }

        return gameStateManager;
    }

    private void TryInstall()
    {
        if (installed) return;

        if (queuedLobbyUI == null || queuedSpawnUI == null)
        {
            return;
        }

        Presentation = new PresentationInstaller().Install(queuedLobbyUI, queuedSpawnUI, -1);

        if (lobbyNetworkService != null && Presentation?.SpawnManager != null)
            lobbyNetworkService.SpawnManagerInstance = Presentation.SpawnManager;

        installed = true;
    }

    private void HandlePlayerJoined(PlayerSession player)
    {
        try
        {
            if (Presentation?.SpawnUI != null)
                Presentation.SpawnUI.OnPlayerAssigned(player.Id, player.PlayerType);

            adminUI?.AddPlayerEntry(player.Id);

            TryShowLobbyForLocalIfNeeded(player.Id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error in HandlePlayerJoined: {e.Message}");
        }
    }

    private void HandlePlayerLeft(int playerId)
    {
        try
        {
            Presentation?.SpawnUI?.HandlePlayerDisconnected(playerId);

            adminUI?.RemovePlayerEntry(playerId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error in HandlePlayerLeft: {e.Message}");
        }
    }

    private void HandlePlayerIdAssigned(int id)
    {
        try
        {
            Presentation?.SpawnManager?.SetLocalPlayerId(id);
            Presentation?.LobbyUI?.SetLocalPlayerId(id);
            Presentation?.LobbyUI?.ResetReadyState();

            TryShowLobbyForLocalIfNeeded(id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error in HandlePlayerIdAssigned: {e.Message}");
        }
    }

    private void HandleLocalJoinAccepted(int id)
    {
        try
        {
            TryShowLobbyForLocalIfNeeded(id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error in HandleLocalJoinAccepted: {e.Message}");
        }
    }

    private void HandleClientDisconnected()
    {
        try
        {
            Presentation?.LobbyUI?.ResetAllToMain();
            var players = appBootstrap?.Services?.LobbyManager?.GetAllPlayers();
            if (players != null)
            {
                foreach (var p in players)
                    Presentation?.SpawnUI?.HandlePlayerDisconnected(p.Id);
            }

            adminUI?.ClearAll();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error in HandleClientDisconnected: {e.Message}");
        }
    }

    private void TryShowLobbyForLocalIfNeeded(int id)
    {
        try
        {
            if (Presentation?.LobbyUI == null || appBootstrap?.Services == null) return;

            if (shouldSuppressShowForLocal != null && shouldSuppressShowForLocal())
            {
                return;
            }

            var clientState = networkBootstrap?.Services?.ClientState;
            if (clientState == null) return;
            if (clientState.PlayerId != id) return;

            var exists = appBootstrap.Services.LobbyManager.GetAllPlayers()?.Any(p => p.Id == id) ?? false;
            if (!exists)
            {
                return;
            }

            Presentation.LobbyUI.ShowLobbyPanel();
            Presentation.LobbyUI.SetConnected(true);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error in TryShowLobbyForLocalIfNeeded: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        try
        {
            if (appBootstrap != null)
            {
                appBootstrap.OnPlayerJoined -= HandlePlayerJoined;
                appBootstrap.OnPlayerLeft -= HandlePlayerLeft;
            }

            if (networkBootstrap != null)
            {
                networkBootstrap.OnPlayerIdAssigned -= HandlePlayerIdAssigned;
                networkBootstrap.OnLocalJoinAccepted -= HandleLocalJoinAccepted;
                networkBootstrap.OnClientDisconnected -= HandleClientDisconnected;
            }

            if (lobbyNetworkService != null)
            {
                lobbyNetworkService.OnStartGameReceived -= HandleStartGameReceived;
                lobbyNetworkService.OnReturnToLobbyReceived -= HandleReturnToLobby; 
            }
        }
        catch { }
    }

    private int network_bootstrap_clientstate_id_fallback()
    {
        try
        {
            return networkBootstrap?.Services?.ClientState?.PlayerId ?? -1;
        }
        catch
        {
            return -1;
        }
    }
}