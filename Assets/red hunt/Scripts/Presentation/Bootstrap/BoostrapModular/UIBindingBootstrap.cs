using System;
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
    private GameUI gameUI;

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

    private void DebugLog(string msg) => Debug.Log($"[UIBinding] {msg}");


    public void Bind(LobbyUI ui, AdminUI admin, NetworkBootstrap networkBootstrap, ApplicationBootstrap appBootstrap, PresentationBootstrap presentationBootstrap)
    {
        lobbyUI = ui;
        adminUI = admin;
        network = networkBootstrap ?? throw new ArgumentNullException(nameof(networkBootstrap));
        presentation = presentationBootstrap ?? throw new ArgumentNullException(nameof(presentationBootstrap));
        appServicesProvider = appBootstrap ?? throw new ArgumentNullException(nameof(appBootstrap));

        lobbyNetworkService = network.GetLobbyNetworkService();

        CreateNamedHandlers();

        presentation.RegisterShowSuppressionPredicate(() => lastSelectionIsHost);


        network.OnClientDisconnected -= HandleNetworkDisconnected;
        network.OnClientDisconnected += HandleNetworkDisconnected;

        network.OnPlayerIdAssigned -= HandleNetworkPlayerIdAssigned;
        network.OnPlayerIdAssigned += HandleNetworkPlayerIdAssigned;

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

        DebugLog("Bind completado.");
    }

    private void HandleNetworkDisconnected()
    {
        DebugLog("Network disconnected -> limpiando estado local");
        clientConnected = false;
        lastSelectionIsHost = false;

        try
        {
            network?.Services?.ClientState?.ClearPendingPlayerType();
        }
        catch { }

        UpdateStartButtonAvailability();
    }

    private void HandleNetworkPlayerIdAssigned(int id)
    {
        DebugLog($"Network asignó playerId {id}");
    }

    private void HandleNetworkLocalJoinAccepted(int id)
    {
        DebugLog($"Network local join accepted {id}");
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
                DebugLog($"Jugador local ({localId}) fue removido del lobby -> limpiando estado local");
                clientConnected = false;
                lastSelectionIsHost = false;
                try { network?.Services?.ClientState?.ClearPendingPlayerType(); } catch { }
            }

            UpdateStartButtonAvailability();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UIBinding] Error en HandleAppPlayerLeft: {e.Message}");
        }
    }

    private void HandleAppPlayerJoined(PlayerSession player)
    {
        try
        {
            DebugLog($"Application reported PlayerJoined: {player.Id} ({player.PlayerType})");
            UpdateStartButtonAvailability();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UIBinding] Error en HandleAppPlayerJoined: {e.Message}");
        }
    }

    public async Task StartHostFlow(int port)
    {
        if (network == null) throw new InvalidOperationException("NetworkBootstrap no inyectado");
        if (presentation?.Presentation?.LobbyUI == null) throw new InvalidOperationException("Presentation no inicializada");

        DebugLog($"StartHostFlow port={port}");
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

    public async Task<bool> StartClientFlow(string ip, int port)
    {
        if (network == null) throw new InvalidOperationException("NetworkBootstrap no inyectado");
        DebugLog($"StartClientFlow ip={ip} port={port}");

        bool success = await network.ConnectToServer(ip, port);
        if (!success)
        {
            clientConnected = false;
            return false;
        }

        clientConnected = true;
        lobbyNetworkService?.SetIsHost(false);
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
            DebugLog("OnHostChosen recibido (intención host)");
            lastSelectionIsHost = true;
        };

        lobby_OnJoinChosen = () =>
        {
            DebugLog("OnJoinChosen recibido (intención join)");
            lastSelectionIsHost = false;
        };

        lobby_OnConfirmRole = async (role) =>
        {
            DebugLog($"Role confirmado: {role} (lastSelectionIsHost={lastSelectionIsHost}, serverStarted={serverStarted}, clientConnected={clientConnected})");

            if (lastSelectionIsHost)
            {
                if (!serverStarted)
                {
                    DebugLog("Iniciando host (StartServer) desde OnConfirmRole");
                    try
                    {
                        await StartHostFlow(presentation.Presentation.LobbyUI.Port);
                        presentation.Presentation.LobbyUI.SetIsHost(true);
                        presentation.Presentation.LobbyUI.ShowLobbyPanel();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Error iniciando host desde OnConfirmRole: {ex.Message}");
                        presentation.Presentation?.LobbyUI?.ResetAllToMain();
                        adminUI?.ClearAll();
                        serverStarted = false;
                        return;
                    }
                }
                else
                {
                    DebugLog("Host ya iniciado, no reiniciando server");
                    presentation.Presentation.LobbyUI.SetIsHost(true);
                    presentation.Presentation.LobbyUI.ShowLobbyPanel();
                }
            }
            else
            {
                if (!clientConnected)
                {
                    DebugLog("Conectando cliente (ConnectToServer) desde OnConfirmRole");
                    try
                    {
                        network.Services.ClientState.SetPendingPlayerType(role.ToString());
                        await StartClientFlow(lobbyUI.IpAddress, lobbyUI.Port);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Error conectando cliente desde OnConfirmRole: {ex.Message}");
                        presentation.Presentation?.LobbyUI?.ResetAllToMain();
                        adminUI?.ClearAll();
                        clientConnected = false;
                        try { network.Services.ClientState.ClearPendingPlayerType(); } catch { }
                        return;
                    }
                }
                else
                {
                    DebugLog("Cliente ya conectado, usando conexión existente");
                }
            }

            try
            {
                DebugLog($"Llamando JoinLobby con role {role}");
                lobbyNetworkService?.JoinLobby(role.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error en JoinLobby: {ex.Message}");
            }
        };

        lobby_OnCreateLobby = async (ip, port) =>
        {
            DebugLog($"Crear Lobby {ip}:{port}");

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
            else
            {
                DebugLog("StartServer solicitado pero serverStarted ya es true; ignorando");
            }
        };

        lobby_OnJoinLobby = async (ip, port) =>
        {
            DebugLog($"Unirse a Lobby {ip}:{port}");

            if (!clientConnected)
            {
                try
                {
                    bool success = await network.ConnectToServer(ip, port);
                    if (!success)
                    {
                        Debug.LogWarning("Conexión al servidor fallida en OnJoinLobby");
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
                    Debug.LogWarning($"Error al conectar cliente en OnJoinLobby: {ex.Message}");
                    presentation.Presentation?.LobbyUI?.ResetAllToMain();
                    adminUI?.ClearAll();
                    clientConnected = false;
                }
            }
            else
            {
                DebugLog("ConnectToServer solicitado pero clientConnected ya es true; ignorando");
            }
        };

        lobby_OnLeaveLobby = async () =>
        {
            DebugLog("Usuario solicitó abandonar el lobby (voluntario)");

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
                Debug.LogWarning($"Error limpiando spawns tras LeaveLobby: {e.Message}");
            }

            clientConnected = false;

            UpdateStartButtonAvailability();
        };

        lobby_OnShutdownServer = async () =>
        {
            DebugLog("Host solicitó apagar el servidor desde la UI");

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
                Debug.LogWarning($"Error limpiando spawns tras ShutdownServer: {e.Message}");
            }

            serverStarted = false;
            clientConnected = false;

            UpdateStartButtonAvailability();
        };

        admin_OnKickRequested = async (targetId) =>
        {
            DebugLog($"Kick solicitado para player {targetId}");
            if (network.Services.AdminService != null)
            {
                await network.Services.AdminService.KickPlayer(targetId);
            }
        };

        gameUI_OnLeaveLobby = async () =>
        {
            DebugLog("GameUI: Cliente solicitó abandonar desde la escena de juego");
            await lobbyNetworkService?.LeaveLobby();
            
            // ⭐ NUEVO: Notificar que vamos a volver desde Game
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
                Debug.LogWarning($"Error limpiando spawns: {e.Message}");
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
            
            DebugLog("Host y clientes volviendo al lobby");
        };
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

        DebugLog("LobbyUI eventos vinculados");
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

        // === Conectar LatencyService ===
        if (network?.Services?.LatencyService != null)
        {
            ui.SetLatencyService(network.Services.LatencyService);
            DebugLog("LatencyService conectado a AdminUI");
        }
        else
        {
            DebugLog("⚠️ LatencyService no disponible en AdminUI");
        }

        DebugLog("AdminUI eventos vinculados");
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
    }

    private void BindGameUI(GameUI ui)
    {
        if (ui == null) return;

        UnbindGameUI(ui);

        ui.OnLeaveLobby -= gameUI_OnLeaveLobby;
        ui.OnLeaveLobby += gameUI_OnLeaveLobby;

        ui.OnReturnToLobby -= gameUI_OnReturnToLobby;
        ui.OnReturnToLobby += gameUI_OnReturnToLobby;

        DebugLog("GameUI eventos vinculados");
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
            Debug.LogWarning($"[UIBinding] Error actualizando StartButtonAvailability: {e.Message}");
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
                network.OnPlayerIdAssigned -= HandleNetworkPlayerIdAssigned;
                network.OnLocalJoinAccepted -= HandleNetworkLocalJoinAccepted;
            }

            if (appServicesProvider != null)
            {
                appServicesProvider.OnPlayerLeft -= HandleAppPlayerLeft;
                appServicesProvider.OnPlayerJoined -= HandleAppPlayerJoined;
            }
        }
        catch { }
    }

    public async Task RequestStartGame(string sceneName)
    {
        try
        {
            Debug.Log($"[UIBinding] RequestStartGame invoked for scene '{sceneName}'");
            await lobbyNetworkService?.StartGame(sceneName);

            if (!string.IsNullOrEmpty(sceneName))
            {
                try
                {
                    GameManager.ChangeScene(sceneName);
                    GameManager.SetCursorVisible(false);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UIBinding] Error cambiando escena local en RequestStartGame: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UIBinding] Error en RequestStartGame: {e.Message}");
        }
    }

    public void HandleExternalStartRequest(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[UIBinding] HandleExternalStartRequest: sceneName vacío.");
            return;
        }
         Debug.Log($"[UIBinding] HandleExternalStartRequest recibido para escena '{sceneName}'");
        _ = RequestStartGame(sceneName);
    }
}
