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
    private UIBindingBootstrap uiBindingBootstrap;

    private AdminUI adminUI;

    private Func<bool> shouldSuppressShowForLocal;

    private bool isReturningFromGameScene = false;
    
    // ⭐ NUEVO: Referencia al GameStateManager
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

            Debug.Log("[PresentationBootstrap] ✅ SpawnUI re-registrada con spawnParents SEPARADOS");

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
                            Debug.LogWarning($"[PresentationBootstrap] Error al repoblar player {p.Id}: {exSpawn.Message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PresentationBootstrap] Error repoblando spawns: {e.Message}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error re-registrando SpawnUI: {e.Message}");
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
            Debug.LogWarning($"[PresentationBootstrap] No se pudo asignar SpawnManager a LobbyNetworkService: {e.Message}");
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

            Debug.Log("[PresentationBootstrap] ✅ LobbyUI re-registrada.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error re-registrando LobbyUI: {e.Message}");
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

        networkBootstrap.OnPlayerIdAssigned += HandlePlayerIdAssigned;
        networkBootstrap.OnLocalJoinAccepted += HandleLocalJoinAccepted;
        networkBootstrap.OnClientDisconnected += HandleClientDisconnected;

        if (lobbyNetworkService != null)
        {
            lobbyNetworkService.OnStartGameReceived += HandleStartGameReceived;
            lobbyNetworkService.OnReturnToLobbyReceived += HandleReturnToLobby;  
        }

        if (installed && LobbyNetworkServiceValid())
        {
            lobbyNetworkService.SpawnManagerInstance = Presentation.SpawnManager;
        }
    }

    private bool LobbyNetworkServiceValid()
    {
        return lobbyNetworkService != null && Presentation?.SpawnManager != null;
    }

    private void HandleStartGameReceived(string sceneName)
    {
        try
        {
            Debug.Log($"[PresentationBootstrap] START_GAME recibido (cliente) -> cambiando a escena '{sceneName}'");
            
            if (!string.IsNullOrEmpty(sceneName))
            {
                if (uiBindingBootstrap != null)
                {
                    Debug.Log("[PresentationBootstrap] Delegando a UIBindingBootstrap.HandleExternalStartRequest()");
                    uiBindingBootstrap.HandleExternalStartRequest(sceneName);
                }
                else
                {
                    Debug.LogWarning("[PresentationBootstrap] UIBindingBootstrap no está asignado, cambiando escena sin desactivar cursor");
                    GameManager.ChangeScene(sceneName);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en HandleStartGameReceived: {e.Message}");
        }
    }

    private void HandleReturnToLobby()
    {
        try
        {
            Debug.Log("[PresentationBootstrap] RETURN_TO_LOBBY recibido - cambiando a escena Lobby");
            
            GameManager.ChangeScene("Lobby");

            GameManager.SetCursorVisible(true);

            Debug.Log("[PresentationBootstrap] ✅ Escena Lobby cargada, LobbyPanel se mostrará al registrarse");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en HandleReturnToLobby: {e.Message}");
        }
    }

    public void RegisterShowSuppressionPredicate(Func<bool> predicate)
    {
        shouldSuppressShowForLocal = predicate;
    }

    // ⭐ NUEVO: Crear GameStateManager cuando sea necesario
    public GameStateManager GetOrCreateGameStateManager()
    {
        if (gameStateManager == null)
        {
            gameStateManager = FindFirstObjectByType<GameStateManager>();
            
            if (gameStateManager == null)
            {
                var gsManagerGO = new GameObject("[GameStateManager]");
                gameStateManager = gsManagerGO.AddComponent<GameStateManager>();
                Debug.Log("[PresentationBootstrap] ✅ GameStateManager creado en escena");
            }
            else
            {
                Debug.Log("[PresentationBootstrap] ✅ GameStateManager encontrado en escena");
            }
        }

        return gameStateManager;
    }

    private void TryInstall()
    {
        if (installed) return;

        if (queuedLobbyUI == null || queuedSpawnUI == null)
        {
            Debug.Log("[PresentationBootstrap] UIs incompletas, esperando registro.");
            return;
        }

        Presentation = new PresentationInstaller().Install(queuedLobbyUI, queuedSpawnUI, -1);

        if (lobbyNetworkService != null && Presentation?.SpawnManager != null)
            lobbyNetworkService.SpawnManagerInstance = Presentation.SpawnManager;

        installed = true;
        Debug.Log("[PresentationBootstrap] PresentationServices instaladas.");
    }

    private void HandlePlayerJoined(PlayerSession player)
    {
        try
        {
            Debug.Log($"[PresentationBootstrap] Player Joined: {player.Id}");
            if (Presentation?.SpawnUI != null)
                Presentation.SpawnUI.OnPlayerAssigned(player.Id, player.PlayerType);

            adminUI?.AddPlayerEntry(player.Id);

            TryShowLobbyForLocalIfNeeded(player.Id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en HandlePlayerJoined: {e.Message}");
        }
    }

    private void HandlePlayerLeft(int playerId)
    {
        try
        {
            Debug.Log($"[PresentationBootstrap] Player Left: {playerId}");
            Presentation?.SpawnUI?.HandlePlayerDisconnected(playerId);

            adminUI?.RemovePlayerEntry(playerId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en HandlePlayerLeft: {e.Message}");
        }
    }

    private void HandlePlayerIdAssigned(int id)
    {
        try
        {
            Debug.Log($"[PresentationBootstrap] PlayerId asignado: {id}");
            Presentation?.SpawnManager?.SetLocalPlayerId(id);
            Presentation?.LobbyUI?.SetLocalPlayerId(id);
            Presentation?.LobbyUI?.ResetReadyState();

            TryShowLobbyForLocalIfNeeded(id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en HandlePlayerIdAssigned: {e.Message}");
        }
    }

    private void HandleLocalJoinAccepted(int id)
    {
        try
        {
            Debug.Log($"[PresentationBootstrap] LocalJoinAccepted para id: {id}");
            TryShowLobbyForLocalIfNeeded(id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en HandleLocalJoinAccepted: {e.Message}");
        }
    }

    private void HandleClientDisconnected()
    {
        try
        {
            Debug.Log("[PresentationBootstrap] Cliente desconectado - limpiando UI");

            Presentation?.LobbyUI?.ResetAllToMain();
            // limpiar spawns
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
            Debug.LogWarning($"[PresentationBootstrap] Error en HandleClientDisconnected: {e.Message}");
        }
    }

    private void TryShowLobbyForLocalIfNeeded(int id)
    {
        try
        {
            if (Presentation?.LobbyUI == null || appBootstrap?.Services == null) return;

            if (shouldSuppressShowForLocal != null && shouldSuppressShowForLocal())
            {
                Debug.Log("[PresentationBootstrap] Supresión de mostrar lobby para local activada; omitiendo ShowLobbyPanel");
                return;
            }

            var clientState = networkBootstrap?.Services?.ClientState;
            if (clientState == null) return;
            if (clientState.PlayerId != id) return;

            var exists = appBootstrap.Services.LobbyManager.GetAllPlayers()?.Any(p => p.Id == id) ?? false;
            if (!exists)
            {
                Debug.Log($"[PresentationBootstrap] player {id} no está aún en LobbyManager, esperando broadcast");
                return;
            }

            Presentation.LobbyUI.ShowLobbyPanel();
            Presentation.LobbyUI.SetConnected(true);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en TryShowLobbyForLocalIfNeeded: {e.Message}");
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