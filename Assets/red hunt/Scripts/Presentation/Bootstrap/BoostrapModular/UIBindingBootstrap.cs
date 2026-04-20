using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class UIBindingBootstrap : MonoBehaviour
{
    private LobbyUI lobbyUI;
    private AdminUI adminUI;
    private NetworkBootstrap network;
    private PresentationBootstrap presentation;
    private ApplicationBootstrap appServicesProvider;
    private LobbyNetworkService lobbyNetworkService;
    private GameNetworkService gameNetworkService;
    private GameUI gameUI;
    private WinBootstrap winBootstrap;

    private bool serverStarted = false;
    private bool clientConnected = false;
    private bool lastSelectionIsHost = false;

    private Action lobby_OnHostChosen;
    private Action lobby_OnJoinChosen;
    private Action<PlayerType> lobby_OnConfirmRole;
    private Action<string, int> lobby_OnCreateLobby;
    private Action<string, int> lobby_OnJoinLobby;
    private Action lobby_OnLeaveLobby;
    private Action lobby_OnShutdownServer;
    private Action<int> admin_OnKickRequested;
    private Action gameUI_OnLeaveLobby;
    private Action gameUI_OnReturnToLobby;
    private Action winUI_OnLeaveLobby;
    private Action winUI_OnReturnToLobby;

    public void Bind(LobbyUI ui, AdminUI admin, NetworkBootstrap networkBootstrap, ApplicationBootstrap appBootstrap, PresentationBootstrap presentationBootstrap)
    {
        lobbyUI = ui;
        adminUI = admin;
        network = networkBootstrap ?? throw new ArgumentNullException(nameof(networkBootstrap));
        presentation = presentationBootstrap ?? throw new ArgumentNullException(nameof(presentationBootstrap));
        appServicesProvider = appBootstrap ?? throw new ArgumentNullException(nameof(appBootstrap));

        lobbyNetworkService = network.GetLobbyNetworkService();
        gameNetworkService = network.GetGameNetworkService();

        CreateNamedHandlers();

        presentation.RegisterShowSuppressionPredicate(() => lastSelectionIsHost);

        network.OnClientDisconnected -= HandleNetworkDisconnected;
        network.OnClientDisconnected += HandleNetworkDisconnected;

        network.OnLocalJoinAccepted -= HandleNetworkLocalJoinAccepted;
        network.OnLocalJoinAccepted += HandleNetworkLocalJoinAccepted;

        appServicesProvider.OnPlayerLeft -= HandleAppPlayerLeft;
        appServicesProvider.OnPlayerLeft += HandleAppPlayerLeft;

        appServicesProvider.OnPlayerJoined -= HandleAppPlayerJoined;
        appServicesProvider.OnPlayerJoined += HandleAppPlayerJoined;

        if (lobbyUI != null)
            BindLobbyUI(lobbyUI);

        if (adminUI != null)
            BindAdminUI(adminUI);
        
        UpdateStartButtonAvailability();

        if (gameNetworkService != null)
        {
            gameNetworkService.OnGameWinReceived -= HandleGameWin;
            gameNetworkService.OnGameWinReceived += HandleGameWin;
        }
    }

    private void HandleNetworkDisconnected()
    {
        clientConnected = false;
        lastSelectionIsHost = false;

        try
        {
            network?.Services?.ClientState?.ClearPendingPlayerType();
        }
        catch { }

        UpdateStartButtonAvailability();
    }

    private void HandleNetworkLocalJoinAccepted(int id)
    {
        clientConnected = true;
        UpdateStartButtonAvailability();
    }

    private void HandleAppPlayerLeft(int playerId)
    {
        try
        {
            var localId = network?.Services?.ClientState?.PlayerId ?? -1;
            if (playerId == localId)
            {
                clientConnected = false;
                lastSelectionIsHost = false;
                try { network?.Services?.ClientState?.ClearPendingPlayerType(); } catch { }
            }

            UpdateStartButtonAvailability();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UIBinding] Error in HandleAppPlayerLeft: {e.Message}");
        }
    }

    private void HandleAppPlayerJoined(PlayerSession player)
    {
        try
        {
            UpdateStartButtonAvailability();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UIBinding] Error in HandleAppPlayerJoined: {e.Message}");
        }
    }

    public async Task StartHostFlow(int port)
    {
        if (network == null) throw new InvalidOperationException("NetworkBootstrap not injected");
        if (presentation?.Presentation?.LobbyUI == null) throw new InvalidOperationException("Presentation not initialized");

        await network.StartServer(port);
        serverStarted = true;

        lobbyNetworkService?.SetIsHost(true);
        gameNetworkService?.SetIsHost(true);

        network.Services.ClientState?.SetIsHost(true);

        presentation.Presentation.LobbyUI.SetIsHost(true);
        adminUI?.SetIsHost(true);

        network.Services.AdminService.SetIsHost(true);
        presentation.Presentation.LobbyUI.SetConnected(true);
        network.Services.SwitchToHost(appServicesProvider.Services.LobbyManager);

        try
        {
            if (network?.Services != null && network.Services.LatencyService == null)
            {
                var installer = new NetworkInstaller();
                var latencyService = installer.CreateAndInitializeLatencyService(
                    network.Services.Server,
                    new AdminPacketBuilder(network.Services.Serializer),
                    network.Services.ConnectionManager
                );

                if (latencyService != null)
                {
                    network.Services.LatencyService = latencyService;
                    adminUI?.SetLatencyService(latencyService);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UIBinding] Error initializing LatencyService: {e.Message}");
        }

        UpdateStartButtonAvailability();
    }

    public async Task<bool> StartClientFlow(string ip, int port)
    {
        if (network == null) throw new InvalidOperationException("NetworkBootstrap not injected");

        bool success = await network.ConnectToServer(ip, port);
        if (!success)
        {
            clientConnected = false;
            return false;
        }

        clientConnected = true;

        lobbyNetworkService?.SetIsHost(false);
        gameNetworkService?.SetIsHost(false);

        network.Services.ClientState?.SetIsHost(false);

        presentation.Presentation.LobbyUI.SetIsHost(false);
        adminUI?.SetIsHost(false);

        network.Services.AdminService.SetIsHost(false);
        presentation.Presentation.LobbyUI.SetConnected(true);
        network.Services.SwitchToClient(appServicesProvider.Services.LobbyManager);

        UpdateStartButtonAvailability();
        return true;
    }

    private void CreateNamedHandlers()
    {
        lobby_OnHostChosen = () =>
        {
            lastSelectionIsHost = true;
        };

        lobby_OnJoinChosen = () =>
        {
            lastSelectionIsHost = false;
        };

        lobby_OnConfirmRole = async (role) =>
        {
            if (lastSelectionIsHost)
            {
                if (!serverStarted)
                {
                    try
                    {
                        await StartHostFlow(presentation.Presentation.LobbyUI.Port);
                        presentation.Presentation.LobbyUI.SetIsHost(true);
                        presentation.Presentation.LobbyUI.ShowLobbyPanel();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[UIBinding] Error starting host from OnConfirmRole: {ex.Message}");
                        presentation.Presentation?.LobbyUI?.ResetAllToMain();
                        adminUI?.ClearAll();
                        serverStarted = false;
                        return;
                    }
                }
                else
                {
                    presentation.Presentation.LobbyUI.SetIsHost(true);
                    presentation.Presentation.LobbyUI.ShowLobbyPanel();
                }
            }
            else
            {
                if (!clientConnected)
                {
                    try
                    {
                        network.Services.ClientState.SetPendingPlayerType(role.ToString());
                        await StartClientFlow(lobbyUI.IpAddress, lobbyUI.Port);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[UIBinding] Error connecting client from OnConfirmRole: {ex.Message}");
                        presentation.Presentation?.LobbyUI?.ResetAllToMain();
                        adminUI?.ClearAll();
                        clientConnected = false;
                        try { network.Services.ClientState.ClearPendingPlayerType(); } catch { }
                        return;
                    }
                }
            }

            try
            {
                lobbyNetworkService?.JoinLobby(role.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UIBinding] Error in JoinLobby: {ex.Message}");
            }
        };

        lobby_OnCreateLobby = async (ip, port) =>
        {
            if (!serverStarted)
            {
                await network.StartServer(port);
                serverStarted = true;

                lobbyNetworkService?.SetIsHost(true);
                presentation.Presentation.LobbyUI.SetIsHost(true);
                adminUI?.SetIsHost(true);

                network.Services.AdminService.SetIsHost(true);
                presentation.Presentation.LobbyUI.SetConnected(true);
                network.Services.SwitchToHost(appServicesProvider.Services.LobbyManager);

                UpdateStartButtonAvailability();
            }
        };

        lobby_OnJoinLobby = async (ip, port) =>
        {
            if (!clientConnected)
            {
                try
                {
                    bool success = await network.ConnectToServer(ip, port);
                    if (!success)
                    {
                        Debug.LogWarning("[UIBinding] Server connection failed in OnJoinLobby");
                        presentation.Presentation?.LobbyUI?.ResetAllToMain();
                        adminUI?.ClearAll();
                        clientConnected = false;
                        return;
                    }

                    clientConnected = true;

                    lobbyNetworkService?.SetIsHost(false);
                    presentation.Presentation.LobbyUI.SetIsHost(false);
                    adminUI?.SetIsHost(false);

                    network.Services.AdminService.SetIsHost(false);
                    presentation.Presentation.LobbyUI.SetConnected(true);
                    network.Services.SwitchToClient(appServicesProvider.Services.LobbyManager);

                    UpdateStartButtonAvailability();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UIBinding] Error connecting client in OnJoinLobby: {ex.Message}");
                    presentation.Presentation?.LobbyUI?.ResetAllToMain();
                    adminUI?.ClearAll();
                    clientConnected = false;
                }
            }
        };

        lobby_OnLeaveLobby = async () =>
        {
            await lobbyNetworkService?.LeaveLobby();

            presentation.Presentation?.LobbyUI?.ResetAllToMain();
            adminUI?.ClearAll();

            try
            {
                var players = appServicesProvider?.Services?.LobbyManager?.GetAllPlayers();
                if (players != null)
                {
                    foreach (var p in players)
                        presentation.Presentation?.SpawnUI?.HandlePlayerDisconnected(p.Id);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UIBinding] Error cleaning spawns after LeaveLobby: {e.Message}");
            }

            clientConnected = false;

            UpdateStartButtonAvailability();
        };

        lobby_OnShutdownServer = async () =>
        {
            await lobbyNetworkService?.LeaveLobby();

            network.Services.Server?.Disconnect();

            presentation.Presentation?.LobbyUI?.ResetAllToMain();
            presentation.Presentation.LobbyUI.SetConnected(false);
            presentation.Presentation.LobbyUI.SetIsHost(false);
            adminUI?.SetIsHost(false);
            adminUI?.ClearAll();

            try
            {
                var players = appServicesProvider?.Services?.LobbyManager?.GetAllPlayers();
                if (players != null)
                {
                    foreach (var p in players)
                        presentation.Presentation?.SpawnUI?.HandlePlayerDisconnected(p.Id);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UIBinding] Error cleaning spawns after ShutdownServer: {e.Message}");
            }

            serverStarted = false;
            clientConnected = false;

            UpdateStartButtonAvailability();
        };

        admin_OnKickRequested = async (targetId) =>
        {
            if (network.Services.AdminService != null)
            {
                await network.Services.AdminService.KickPlayer(targetId);
            }
        };

        gameUI_OnLeaveLobby = async () =>
        {
            await lobbyNetworkService?.LeaveLobby();
            
            presentation.SetReturningFromGameScene(true);
            
            adminUI?.ClearAll();
            
            try
            {
                var players = appServicesProvider?.Services?.LobbyManager?.GetAllPlayers();
                if (players != null)
                {
                    foreach (var p in players)
                        presentation.Presentation?.SpawnUI?.HandlePlayerDisconnected(p.Id);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UIBinding] Error cleaning spawns: {e.Message}");
            }

            clientConnected = false;
            lastSelectionIsHost = false;
            
            GameManager.SetCursorVisible(true);
            GameManager.ChangeScene("Lobby");
            
            UpdateStartButtonAvailability();
        };

        gameUI_OnReturnToLobby = async () =>
        {
            if (lobbyNetworkService != null)
            {
                lobbyNetworkService.ResetGameStarted();  
            }
            
            await lobbyNetworkService?.ReturnAllPlayersToLobby();
            
            presentation.SetReturningFromGameScene(false);
            
            if (gameUI != null)
            {
                gameUI.SetConnected(false);
                gameUI.SetIsHost(false);
            }
            
            if (presentation.Presentation?.LobbyUI != null)
            {
                presentation.Presentation.LobbyUI.SetConnected(true);
                presentation.Presentation.LobbyUI.SetIsHost(true);
            }
            
            GameManager.ChangeScene("Lobby");
        };

        winUI_OnLeaveLobby = async () =>
        {
            await lobbyNetworkService?.LeaveLobby();
            
            presentation.SetReturningFromGameScene(true);
            
            adminUI?.ClearAll();
            
            try
            {
                var players = appServicesProvider?.Services?.LobbyManager?.GetAllPlayers();
                if (players != null)
                {
                    foreach (var p in players)
                        presentation.Presentation?.SpawnUI?.HandlePlayerDisconnected(p.Id);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UIBinding] Error cleaning spawns in WinUI: {e.Message}");
            }

            clientConnected = false;
            lastSelectionIsHost = false;
            
            GameManager.SetCursorVisible(true);
            GameManager.ChangeScene("Lobby");
            
            UpdateStartButtonAvailability();
        };

        winUI_OnReturnToLobby = async () =>
        {
            if (lobbyNetworkService != null)
            {
                lobbyNetworkService.ResetGameStarted();  
            }
            
            await lobbyNetworkService?.ReturnAllPlayersToLobby();
            
            presentation.SetReturningFromGameScene(false);
            
            if (presentation.Presentation?.LobbyUI != null)
            {
                presentation.Presentation.LobbyUI.SetConnected(true);
                presentation.Presentation.LobbyUI.SetIsHost(true);
            }
            
            GameManager.SetCursorVisible(true);
            GameManager.ChangeScene("Lobby");
        };
    }

    private async void HandleGameWin(int winnerId, string winnerType, bool isKillerWin)
    {
        try
        {
            GameManager.ChangeScene("Win");
            GameManager.SetCursorVisible(true);

            await Task.Delay(200);
            
            InitializeWinScene(winnerId, winnerType, isKillerWin);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UIBinding] Error changing to Win scene: {e.Message}");
        }
    }

    private void InitializeWinScene(int winnerId, string winnerType, bool isKillerWin)
    {
        try
        {
            if (winBootstrap == null)
            {
                winBootstrap = GetComponent<WinBootstrap>();
                if (winBootstrap == null)
                {
                    Debug.LogError("[UIBinding] WinBootstrap not found");
                    return;
                }
            }

            winBootstrap.Init(network, appServicesProvider, presentation);

            var winUI = FindFirstObjectByType<WinUI>();
            if (winUI == null)
            {
                Debug.LogError("[UIBinding] WinUI not found in Win scene");
                return;
            }

            var winCameraManager = FindFirstObjectByType<WinCameraManager>();
            if (winCameraManager == null)
            {
                Debug.LogError("[UIBinding] WinCameraManager not found in Win scene");
                return;
            }

            winBootstrap.SetWinUIAndCamera(winUI, winCameraManager);
            winBootstrap.SetWinData(winnerId, winnerType, isKillerWin);

            BindWinUI(winUI);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIBinding] Error initializing Win scene: {e.Message}\n{e.StackTrace}");
        }
    }

    private void BindWinUI(WinUI ui)
    {
        if (ui == null) return;

        UnbindWinUI(ui);

        ui.OnLeaveLobby -= winUI_OnLeaveLobby;
        ui.OnLeaveLobby += winUI_OnLeaveLobby;

        ui.OnReturnToLobby -= winUI_OnReturnToLobby;
        ui.OnReturnToLobby += winUI_OnReturnToLobby;
    }

    private void UnbindWinUI(WinUI ui)
    {
        if (ui == null) return;
        try
        {
            ui.OnLeaveLobby -= winUI_OnLeaveLobby;
            ui.OnReturnToLobby -= winUI_OnReturnToLobby;
        }
        catch { }
    }

    private void BindLobbyUI(LobbyUI ui)
    {
        if (ui == null) return;

        UnbindLobbyUI(ui);

        ui.OnHostChosen -= lobby_OnHostChosen;
        ui.OnHostChosen += lobby_OnHostChosen;

        ui.OnJoinChosen -= lobby_OnJoinChosen;
        ui.OnJoinChosen += lobby_OnJoinChosen;

        ui.OnConfirmRole -= lobby_OnConfirmRole;
        ui.OnConfirmRole += lobby_OnConfirmRole;

        ui.OnCreateLobby -= lobby_OnCreateLobby;
        ui.OnCreateLobby += lobby_OnCreateLobby;

        ui.OnJoinLobby -= lobby_OnJoinLobby;
        ui.OnJoinLobby += lobby_OnJoinLobby;

        ui.OnLeaveLobby -= lobby_OnLeaveLobby;
        ui.OnLeaveLobby += lobby_OnLeaveLobby;

        ui.OnShutdownServer -= lobby_OnShutdownServer;
        ui.OnShutdownServer += lobby_OnShutdownServer;
    }

    private void UnbindLobbyUI(LobbyUI ui)
    {
        if (ui == null) return;
        try
        {
            ui.OnHostChosen -= lobby_OnHostChosen;
            ui.OnJoinChosen -= lobby_OnJoinChosen;
            ui.OnConfirmRole -= lobby_OnConfirmRole;
            ui.OnCreateLobby -= lobby_OnCreateLobby;
            ui.OnJoinLobby -= lobby_OnJoinLobby;
            ui.OnLeaveLobby -= lobby_OnLeaveLobby;
            ui.OnShutdownServer -= lobby_OnShutdownServer;
        }
        catch { }
    }

    private void BindAdminUI(AdminUI ui)
    {
        if (ui == null) return;

        UnbindAdminUI(ui);

        ui.OnKickRequested -= admin_OnKickRequested;
        ui.OnKickRequested += admin_OnKickRequested;

        LatencyService latencyService = null;
        if (network?.Services?.LatencyService != null)
        {
            latencyService = network.Services.LatencyService;
        }
        else
        {
            latencyService = FindFirstObjectByType<LatencyService>();
        }

        if (latencyService != null)
        {
            ui.SetLatencyService(latencyService);
        }
    }

    private void UnbindAdminUI(AdminUI ui)
    {
        if (ui == null) return;
        try
        {
            ui.OnKickRequested -= admin_OnKickRequested;
        }
        catch { }
    }

    public void SetGameUI(GameUI ui)
    {
        if (ui == null)
        {
            UnbindGameUI(gameUI);
            gameUI = null;
            return;
        }

        gameUI = ui;
        BindGameUI(gameUI);
        
        BindCluesUI();
    }

    private void BindCluesUI()
    {
        if (gameNetworkService == null)
        {
            Debug.LogWarning("[UIBinding] GameNetworkService not available for CluesUI");
            return;
        }

        try
        {
            var cluesDisplay = FindFirstObjectByType<EscapistCluesDisplay>();
            if (cluesDisplay == null)
            {
                Debug.LogWarning("[UIBinding] EscapistCluesDisplay not found in scene");
                return;
            }

            gameNetworkService.OnEscapistsCluesSnapshot -= cluesDisplay.OnSnapshotReceived;
            gameNetworkService.OnEscapistsCluesSnapshot += cluesDisplay.OnSnapshotReceived;

            gameNetworkService.OnEscapistClueCollected -= HandleEscapistClueCollected;
            gameNetworkService.OnEscapistClueCollected += HandleEscapistClueCollected;

            void HandleEscapistClueCollected(int escapistId, string clueId)
            {
                try
                {
                    var localPlayerId = network?.Services?.ClientState?.PlayerId ?? -1;
                    if (localPlayerId > 0 && escapistId == localPlayerId)
                    {
                        cluesDisplay.OnClueObtained(escapistId, clueId);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UIBinding] Error in HandleEscapistClueCollected: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIBinding] Error in BindCluesUI: {e.Message}");
        }
    }

    private void BindGameUI(GameUI ui)
    {
        if (ui == null) return;

        UnbindGameUI(ui);

        ui.OnLeaveLobby -= gameUI_OnLeaveLobby;
        ui.OnLeaveLobby += gameUI_OnLeaveLobby;

        ui.OnReturnToLobby -= gameUI_OnReturnToLobby;
        ui.OnReturnToLobby += gameUI_OnReturnToLobby;
    }

    private void UnbindGameUI(GameUI ui)
    {
        if (ui == null) return;
        try
        {
            ui.OnLeaveLobby -= gameUI_OnLeaveLobby;
            ui.OnReturnToLobby -= gameUI_OnReturnToLobby;
        }
        catch { }
    }

    private void UpdateStartButtonAvailability()
    {
        try
        {
            if (presentation?.Presentation?.LobbyUI == null || appServicesProvider?.Services?.LobbyManager == null) return;

            var players = appServicesProvider.Services.LobbyManager.GetAllPlayers() ?? Enumerable.Empty<PlayerSession>();

            bool anyKiller = players.Any(p => p.PlayerType == PlayerType.Killer.ToString());
            bool anyEscapist = players.Any(p => p.PlayerType == PlayerType.Escapist.ToString());

            bool canStart = (lobbyNetworkService != null && lobbyNetworkService.IsHost) && anyKiller && anyEscapist;

            presentation.Presentation.LobbyUI.SetStartInteractable(canStart);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UIBinding] Error updating StartButtonAvailability: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        try
        {
            if (lobbyUI != null) UnbindLobbyUI(lobbyUI);
            if (adminUI != null) UnbindAdminUI(adminUI);

            if (network != null)
            {
                network.OnClientDisconnected -= HandleNetworkDisconnected;
                network.OnLocalJoinAccepted -= HandleNetworkLocalJoinAccepted;
            }

            if (appServicesProvider != null)
            {
                appServicesProvider.OnPlayerLeft -= HandleAppPlayerLeft;
                appServicesProvider.OnPlayerJoined -= HandleAppPlayerJoined;
            }

            if (gameNetworkService != null)
            {
                gameNetworkService.OnGameWinReceived -= HandleGameWin;
            }
        }
        catch { }
    }

    public async Task RequestStartGame(string sceneName)
    {
        try
        {
            await lobbyNetworkService?.StartGame(sceneName);

            if (!string.IsNullOrEmpty(sceneName))
            {
                try
                {
                    var isHost = network?.Services?.ClientState?.IsHost ?? false;
                    GameManager.IsHost = isHost;
                    
                    GameManager.ChangeScene(sceneName);
                    GameManager.SetCursorVisible(false);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UIBinding] Error changing local scene in RequestStartGame: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UIBinding] Error in RequestStartGame: {e.Message}");
        }
    }

    public void HandleExternalStartRequest(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[UIBinding] HandleExternalStartRequest: sceneName is empty.");
            return;
        }
        _ = RequestStartGame(sceneName);
    }
}
