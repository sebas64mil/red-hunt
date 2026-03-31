using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class LobbyBootstrap : MonoBehaviour
{
    public static LobbyBootstrap Instance { get; private set; }

    [Header("Network")]
    [SerializeField] private Server server;
    [SerializeField] private Client client;

    [Header("Presentation")]
    [SerializeField] private LobbyUI lobbyUI;
    [SerializeField] private SpawnUI spawnUI;
    [SerializeField] private AdminUI adminUI;

    private NetworkServices networkServices;
    private ApplicationServices applicationServices;
    private PresentationServices presentationServices;

    private LobbyNetworkService lobbyNetworkService;

    private bool serverStarted = false;
    private bool clientConnected = false;
    private bool lastSelectionIsHost = false;

    private Action onPlayerIdAssignedHandler;
    private Action<int> onLocalJoinAcceptedHandler;

    private Action lobby_OnHostChosen;
    private Action lobby_OnJoinChosen;
    private Action<PlayerType> lobby_OnConfirmRole;
    private Action<string, int> lobby_OnCreateLobby;
    private Action<string, int> lobby_OnJoinLobby;
    private Action lobby_OnStartGame;
    private Action lobby_OnLeaveLobby;
    private Action lobby_OnShutdownServer;

    private Action<int> admin_OnKickRequested;

    private bool presentationInstalled = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log("[GameBootstrap] Otra instancia detectada, destruyendo esta.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[GameBootstrap] ============ INICIANDO SISTEMA ============");

        try
        {
            // ==================== FASE 1: APPLICATION ====================
            Debug.Log("[GameBootstrap] Fase 1: Application...");
            var applicationInstaller = new ApplicationInstaller();
            applicationServices = applicationInstaller.Install();

            // ==================== FASE 2: LOBBY NETWORK ====================
            Debug.Log("[GameBootstrap] Fase 2: LobbyNetworkService...");
            lobbyNetworkService = gameObject.AddComponent<LobbyNetworkService>();

            // ==================== FASE 3: NETWORK ====================
            Debug.Log("[GameBootstrap] Fase 3: Network...");
            networkServices = new NetworkInstaller()
                .Install(server, client, applicationServices.LobbyManager, lobbyNetworkService, false);

            CreateNamedHandlers();

            TryInstallPresentationIfPossible();

            networkServices.ClientState.OnPlayerIdAssigned += id =>
            {
                try
                {
                    Debug.Log($"[Bootstrap] PlayerId asignado: {id}");
                    if (presentationServices?.SpawnManager != null)
                        presentationServices.SpawnManager.SetLocalPlayerId(id);

                    if (presentationServices?.LobbyUI != null)
                        presentationServices.LobbyUI.SetLocalPlayerId(id);

                    if (presentationServices?.LobbyUI != null)
                        presentationServices.LobbyUI.ResetReadyState();

                    TryShowLobbyForLocal(id);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Bootstrap] Error en OnPlayerIdAssigned handler: {e.Message}");
                }
            };

            lobbyNetworkService.OnLocalJoinAccepted += id =>
            {
                Debug.Log($"[Bootstrap] Join confirmado por server para id: {id}");
                TryShowLobbyForLocal(id);
            };

            // ==================== FASE 5: CONEXIONES ====================
            Debug.Log("[GameBootstrap] Fase 5: Conectando capas...");
            ConnectLayers();

            Debug.Log("[GameBootstrap] SISTEMA INICIALIZADO CORRECTAMENTE");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameBootstrap] ERROR: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private void TryInstallPresentationIfPossible()
    {
        if (presentationInstalled) return;

        if (lobbyUI == null || spawnUI == null)
        {
            Debug.Log("[GameBootstrap] Presentation UI no completa aún, esperando registro si procede.");
            return;
        }

        Debug.Log("[GameBootstrap] Fase 4: Presentation...");
        presentationServices = new PresentationInstaller()
            .Install(lobbyUI, spawnUI, -1);

        lobbyNetworkService.SpawnManagerInstance = presentationServices.SpawnManager;

        presentationInstalled = true;
    }

    private void TryShowLobbyForLocal(int id)
    {
        try
        {
            if (lastSelectionIsHost) return;

            if (networkServices?.ClientState == null) return;
            if (networkServices.ClientState.PlayerId != id) return;

            var exists = applicationServices?.LobbyManager?.GetAllPlayers()?.Any(p => p.Id == id) ?? false;
            if (!exists)
            {
                Debug.Log($"[Bootstrap] TryShowLobbyForLocal: player {id} no está aún en LobbyManager, esperando broadcast");
                return;
            }

            presentationServices.LobbyUI.ShowLobbyPanel();
            presentationServices.LobbyUI.SetConnected(true);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Bootstrap] Error en TryShowLobbyForLocal: {e.Message}");
        }
    }

    private void ConnectLayers()
    {
        // ==================== CLIENT EVENTS ====================
        networkServices.Client.OnDisconnected += () =>
        {
            Debug.Log("[Bootstrap] Cliente desconectado - limpiando UI y estado local");

            presentationServices?.LobbyUI?.ResetAllToMain();

            adminUI?.ClearAll();

            try
            {
                var players = applicationServices?.LobbyManager?.GetAllPlayers();
                if (players != null)
                {
                    foreach (var p in players)
                    {
                        presentationServices?.SpawnUI?.HandlePlayerDisconnected(p.Id);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Bootstrap] Error limpiando spawns al desconectar: {e.Message}");
            }

            clientConnected = false;
        };

        if (presentationServices?.LobbyUI != null)
            BindLobbyUI(presentationServices.LobbyUI);

        if (adminUI != null)
            BindAdminUI(adminUI);

        if (presentationInstalled)
        {
            applicationServices.LobbyManager.OnPlayerJoined += (player) =>
            {
                try
                {
                    Debug.Log($"[Bootstrap] Player Joined: {player.Id}");

                    presentationServices.SpawnUI.OnPlayerAssigned(player.Id, player.PlayerType);

                    adminUI?.AddPlayerEntry(player.Id);

                    TryShowLobbyForLocal(player.Id);

                    // Actualizar disponibilidad del botón Start cuando cambian jugadores
                    UpdateStartButtonAvailability();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Bootstrap] Error en OnPlayerJoined handler: {e.Message}");
                }
            };

            applicationServices.LobbyManager.OnPlayerLeft += (playerId) =>
            {
                try
                {
                    Debug.Log($"[Bootstrap] Player Left: {playerId}");

                    presentationServices.SpawnUI.HandlePlayerDisconnected(playerId);

                    adminUI?.RemovePlayerEntry(playerId);

                    // Actualizar disponibilidad del botón Start cuando cambian jugadores
                    UpdateStartButtonAvailability();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Bootstrap] Error en OnPlayerLeft handler: {e.Message}");
                }
            };
        }

        if (adminUI != null)
            adminUI.SetIsHost(false);

        if (adminUI != null)
            BindAdminUI(adminUI);

        Debug.Log("[GameBootstrap] Capas conectadas");
    }

    // -------------------- Registration API used by UIs --------------------
    public void RegisterLobbyUI(LobbyUI ui)
    {
        if (ui == null) return;
        if (lobbyUI == ui) return;

        // unbind previous if present
        if (lobbyUI != null)
            UnbindLobbyUI(lobbyUI);

        lobbyUI = ui;

        if (presentationInstalled)
        {
            presentationServices.LobbyUI = lobbyUI;
            BindLobbyUI(lobbyUI);
        }
        else
        {
            TryInstallPresentationIfPossible();

            if (presentationInstalled && presentationServices.LobbyUI != null)
                BindLobbyUI(presentationServices.LobbyUI);
        }

        Debug.Log("[GameBootstrap] LobbyUI registrada");
    }

    public void UnregisterLobbyUI(LobbyUI ui)
    {
        if (ui == null) return;
        if (lobbyUI != ui) return;

        UnbindLobbyUI(lobbyUI);
        lobbyUI = null;

        if (presentationServices != null)
            presentationServices.LobbyUI = null;

        Debug.Log("[GameBootstrap] LobbyUI desregistrada");
    }

    public void RegisterSpawnUI(SpawnUI ui)
    {
        if (ui == null) return;
        if (spawnUI == ui) return;

        spawnUI = ui;

        if (presentationInstalled && presentationServices?.SpawnManager != null)
        {
            try
            {
                spawnUI.Init(presentationServices.SpawnManager);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameBootstrap] Error inicializando SpawnUI: {e.Message}");
            }
        }
        else
        {
            TryInstallPresentationIfPossible();
        }

        Debug.Log("[GameBootstrap] SpawnUI registrada");
    }

    public void UnregisterSpawnUI(SpawnUI ui)
    {
        if (ui == null) return;
        if (spawnUI != ui) return;

        spawnUI = null;
        Debug.Log("[GameBootstrap] SpawnUI desregistrada");
    }

    public void RegisterAdminUI(AdminUI ui)
    {
        if (ui == null) return;
        if (adminUI == ui) return;

        if (adminUI != null)
            UnbindAdminUI(adminUI);

        adminUI = ui;

        adminUI.SetIsHost(false);

        BindAdminUI(adminUI);

        Debug.Log("[GameBootstrap] AdminUI registrada");
    }

    public void UnregisterAdminUI(AdminUI ui)
    {
        if (ui == null) return;
        if (adminUI != ui) return;

        UnbindAdminUI(adminUI);
        adminUI = null;

        Debug.Log("[GameBootstrap] AdminUI desregistrada");
    }

    // -------------------- Binding helpers --------------------
    private void CreateNamedHandlers()
    {
        lobby_OnHostChosen = () =>
        {
            Debug.Log("[Bootstrap] OnHostChosen recibido (intención host)");
            lastSelectionIsHost = true;
        };

        lobby_OnJoinChosen = () =>
        {
            Debug.Log("[Bootstrap] OnJoinChosen recibido (intención join)");
            lastSelectionIsHost = false;
        };

        lobby_OnConfirmRole = async (role) =>
        {
            Debug.Log($"[Bootstrap] Role confirmado: {role} (lastSelectionIsHost={lastSelectionIsHost}, serverStarted={serverStarted}, clientConnected={clientConnected})");

            // log estado cliente/pending antes de cambiar nada
            try
            {
                var pending = networkServices?.ClientState?.PendingPlayerType;
                Debug.Log($"[Bootstrap] ClientState.PendingPlayerType (antes) = {(string.IsNullOrEmpty(pending) ? "<null>" : pending)}");
            }
            catch { }

            if (lastSelectionIsHost)
            {
                if (!serverStarted)
                {
                    Debug.Log("[Bootstrap] Iniciando host (StartServer) desde OnConfirmRole");
                    try
                    {
                        await StartHostFlow();
                        presentationServices.LobbyUI.ShowLobbyPanel();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Bootstrap] Error iniciando host desde OnConfirmRole: {ex.Message}");
                        presentationServices?.LobbyUI?.ResetAllToMain();
                        adminUI?.ClearAll();
                        serverStarted = false;
                        return;
                    }
                }
                else
                {
                    Debug.Log("[Bootstrap] Host ya iniciado, no reiniciando server");
                    presentationServices.LobbyUI.ShowLobbyPanel();
                }
            }
            else
            {
                if (!clientConnected)
                {
                    Debug.Log("[Bootstrap] Conectando cliente (ConnectToServer) desde OnConfirmRole");
                    try
                    {
                        networkServices?.ClientState?.SetPendingPlayerType(role.ToString());

                        await StartClientFlow();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Bootstrap] Error conectando cliente desde OnConfirmRole: {ex.Message}");
                        presentationServices?.LobbyUI?.ResetAllToMain();
                        adminUI?.ClearAll();
                        clientConnected = false;

                        try
                        {
                            networkServices?.ClientState?.ClearPendingPlayerType();
                        }
                        catch { }

                        return;
                    }
                }
                else
                {
                    Debug.Log("[Bootstrap] Cliente ya conectado, usando conexión existente");
                }
            }

            try
            {
                Debug.Log($"[Bootstrap] Llamando JoinLobby con role {role}");
                lobbyNetworkService.JoinLobby(role.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Bootstrap] Error en JoinLobby: {ex.Message}");
            }

            try
            {
                var pendingAfter = networkServices?.ClientState?.PendingPlayerType;
                Debug.Log($"[Bootstrap] ClientState.PendingPlayerType (después) = {(string.IsNullOrEmpty(pendingAfter) ? "<null>" : pendingAfter)}");
            }
            catch { }
        };

        lobby_OnCreateLobby = async (ip, port) =>
        {
            Debug.Log($"[Bootstrap] Crear Lobby {ip}:{port}");

            if (!serverStarted)
            {
                await networkServices.Server.StartServer(port);
                serverStarted = true;

                lobbyNetworkService.SetIsHost(true);
                presentationServices.LobbyUI.SetIsHost(true);
                adminUI?.SetIsHost(true);

                networkServices.AdminService.SetIsHost(true);

                presentationServices.LobbyUI.SetConnected(true);

                networkServices.SwitchToHost(applicationServices.LobbyManager);

                // al crear lobby como host, actualizar disponibilidad del StartButton
                UpdateStartButtonAvailability();
            }
            else
            {
                Debug.Log("[Bootstrap] StartServer solicitado pero serverStarted ya es true; ignorando");
            }
        };

        lobby_OnJoinLobby = async (ip, port) =>
        {
            Debug.Log($"[Bootstrap] Unirse a Lobby {ip}:{port}");

            if (!clientConnected)
            {
                try
                {
                    bool success = await networkServices.Client.ConnectToServer(ip, port);
                    if (!success)
                    {
                        Debug.LogWarning("[Bootstrap] Conexión al servidor fallida en OnJoinLobby");
                        presentationServices?.LobbyUI?.ResetAllToMain();
                        adminUI?.ClearAll();
                        clientConnected = false;
                        return;
                    }

                    clientConnected = true;

                    lobbyNetworkService.SetIsHost(false);
                    presentationServices.LobbyUI.SetIsHost(false);
                    adminUI?.SetIsHost(false);

                    networkServices.AdminService.SetIsHost(false);

                    presentationServices.LobbyUI.SetConnected(true);

                    networkServices.SwitchToClient(applicationServices.LobbyManager);

                    // actualizar estado del StartButton (será oculto porque no es host)
                    UpdateStartButtonAvailability();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Bootstrap] Error al conectar cliente en OnJoinLobby: {ex.Message}");
                    presentationServices?.LobbyUI?.ResetAllToMain();
                    adminUI?.ClearAll();
                    clientConnected = false;
                }
            }
            else
            {
                Debug.Log("[Bootstrap] ConnectToServer solicitado pero clientConnected ya es true; ignorando");
            }
        };

        lobby_OnStartGame = async () =>
        {
            Debug.Log("[Bootstrap] Host solicitó START_GAME desde UI");
            await lobbyNetworkService.StartGame();
        };

        lobby_OnLeaveLobby = async () =>
        {
            Debug.Log("[Bootstrap] Usuario solicitó abandonar el lobby (voluntario)");

            await lobbyNetworkService.LeaveLobby();

            presentationServices?.LobbyUI?.ResetAllToMain();

            adminUI?.ClearAll();

            try
            {
                var players = applicationServices?.LobbyManager?.GetAllPlayers();
                if (players != null)
                {
                    foreach (var p in players)
                    {
                        presentationServices?.SpawnUI?.HandlePlayerDisconnected(p.Id);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Bootstrap] Error limpiando spawns tras LeaveLobby: {e.Message}");
            }

            clientConnected = false;
        };

        lobby_OnShutdownServer = async () =>
        {
            Debug.Log("[Bootstrap] Host solicitó apagar el servidor desde la UI");

            await lobbyNetworkService.LeaveLobby();

            networkServices.Server?.Disconnect();

            presentationServices?.LobbyUI?.ResetAllToMain();

            presentationServices.LobbyUI.SetConnected(false);
            presentationServices.LobbyUI.SetIsHost(false);
            adminUI?.SetIsHost(false);
            adminUI?.ClearAll();

            try
            {
                var players = applicationServices?.LobbyManager?.GetAllPlayers();
                if (players != null)
                {
                    foreach (var p in players)
                    {
                        presentationServices?.SpawnUI?.HandlePlayerDisconnected(p.Id);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Bootstrap] Error limpiando spawns tras ShutdownServer: {e.Message}");
            }

            serverStarted = false;
            clientConnected = false;
        };

        admin_OnKickRequested = async (targetId) =>
        {
            Debug.Log($"[Bootstrap] Kick solicitado para player {targetId}");

            if (networkServices.AdminService != null)
            {
                await networkServices.AdminService.KickPlayer(targetId);
            }
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

        ui.OnStartGame -= lobby_OnStartGame;
        ui.OnStartGame += lobby_OnStartGame;

        ui.OnLeaveLobby -= lobby_OnLeaveLobby;
        ui.OnLeaveLobby += lobby_OnLeaveLobby;

        ui.OnShutdownServer -= lobby_OnShutdownServer;
        ui.OnShutdownServer += lobby_OnShutdownServer;

        Debug.Log("[GameBootstrap] LobbyUI eventos vinculados");
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
            ui.OnStartGame -= lobby_OnStartGame;
            ui.OnLeaveLobby -= lobby_OnLeaveLobby;
            ui.OnShutdownServer -= lobby_OnShutdownServer;
        }
        catch (Exception)
        {
        }
    }

    private void BindAdminUI(AdminUI ui)
    {
        if (ui == null) return;

        UnbindAdminUI(ui);

        ui.OnKickRequested -= admin_OnKickRequested;
        ui.OnKickRequested += admin_OnKickRequested;

        Debug.Log("[GameBootstrap] AdminUI eventos vinculados");
    }

    private void UnbindAdminUI(AdminUI ui)
    {
        if (ui == null) return;
        try
        {
            ui.OnKickRequested -= admin_OnKickRequested;
        }
        catch (Exception)
        {
        }
    }

    private async Task StartHostFlow()
    {
        await networkServices.Server.StartServer(presentationServices.LobbyUI.Port);
        serverStarted = true;

        lobbyNetworkService.SetIsHost(true);
        presentationServices.LobbyUI.SetIsHost(true);
        adminUI?.SetIsHost(true);

        networkServices.AdminService.SetIsHost(true);

        presentationServices.LobbyUI.SetConnected(true);

        networkServices.SwitchToHost(applicationServices.LobbyManager);

        // actualizar disponibilidad del StartButton tras cambiar a host
        UpdateStartButtonAvailability();
    }

    private async Task<bool> StartClientFlow()
    {
        bool success = await networkServices.Client.ConnectToServer(lobbyUI.IpAddress, lobbyUI.Port);

        if (!success)
        {
            clientConnected = false;
            return false;
        }

        clientConnected = true;

        lobbyNetworkService.SetIsHost(false);
        presentationServices.LobbyUI.SetIsHost(false);
        adminUI?.SetIsHost(false);

        networkServices.AdminService.SetIsHost(false);

        presentationServices.LobbyUI.SetConnected(true);

        networkServices.SwitchToClient(applicationServices.LobbyManager);

        UpdateStartButtonAvailability();

        return true;
    }

    private void UpdateStartButtonAvailability()
    {
        try
        {
            if (presentationServices?.LobbyUI == null || applicationServices?.LobbyManager == null) return;

            var players = applicationServices.LobbyManager.GetAllPlayers() ?? Enumerable.Empty<PlayerSession>();

            bool anyKiller = players.Any(p => p.PlayerType == PlayerType.Killer.ToString());
            bool anyEscapist = players.Any(p => p.PlayerType == PlayerType.Escapist.ToString());

            bool canStart = (lobbyNetworkService != null && lobbyNetworkService.IsHost) && anyKiller && anyEscapist;

            presentationServices.LobbyUI.SetStartInteractable(canStart);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Bootstrap] Error actualizando StartButtonAvailability: {e.Message}");
        }
    }
}