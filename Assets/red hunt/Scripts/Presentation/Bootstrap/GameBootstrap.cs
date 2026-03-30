using System;
using System.Threading.Tasks;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
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

    private void Awake()
    {
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

            // ==================== FASE 4: PRESENTATION ====================
            Debug.Log("[GameBootstrap] Fase 4: Presentation...");
            presentationServices = new PresentationInstaller()
                .Install(lobbyUI, spawnUI, -1);

            lobbyNetworkService.SpawnManagerInstance = presentationServices.SpawnManager;

            networkServices.ClientState.OnPlayerIdAssigned += (id) =>
            {
                Debug.Log($"[Bootstrap] PlayerId asignado: {id}");
                presentationServices.SpawnManager.SetLocalPlayerId(id);
                presentationServices.LobbyUI.SetLocalPlayerId(id);

                presentationServices.LobbyUI.ResetReadyState();

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

    private void ConnectLayers()
    {
        // ==================== CLIENT EVENTS ====================

        networkServices.Client.OnDisconnected += () =>
        {
            Debug.Log("[Bootstrap] Cliente desconectado - limpiando UI y estado local");

            // Usar el nuevo helper en LobbyUI para restablecer la UI al main panel
            presentationServices?.LobbyUI?.ResetAllToMain();

            // Limpiar admin UI y spawns locales (mantener comportamiento existente)
            adminUI.ClearAll();

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

        // ==================== UI -> NETWORK ====================

        presentationServices.LobbyUI.OnHostChosen += () =>
        {
            Debug.Log("[Bootstrap] OnHostChosen recibido (intención host)");
            lastSelectionIsHost = true;
        };


        presentationServices.LobbyUI.OnJoinChosen += () =>
        {
            Debug.Log("[Bootstrap] OnJoinChosen recibido (intención join)");
            lastSelectionIsHost = false;
        };

        presentationServices.LobbyUI.OnConfirmRole += async (role) =>
        {
            Debug.Log($"[Bootstrap] Role confirmado: {role}");

            presentationServices.LobbyUI.ShowLobbyPanel();

            if (lastSelectionIsHost)
            {
                if (!serverStarted)
                {
                    Debug.Log("[Bootstrap] Iniciando host (StartServer) desde OnConfirmRole");
                    await StartHostFlow();
                }
                else
                {
                    Debug.Log("[Bootstrap] Host ya iniciado, no reiniciando server");
                }
            }
            else
            {
                if (!clientConnected)
                {
                    Debug.Log("[Bootstrap] Conectando cliente (ConnectToServer) desde OnConfirmRole");
                    await StartClientFlow();
                }
                else
                {
                    Debug.Log("[Bootstrap] Cliente ya conectado, usando conexión existente");
                }
            }

            lobbyNetworkService.JoinLobby(role.ToString());
        };
        presentationServices.LobbyUI.OnCreateLobby += async (ip, port) =>
        {
            Debug.Log($"[Bootstrap] Crear Lobby {ip}:{port}");

            if (!serverStarted)
            {
                await networkServices.Server.StartServer(port);
                serverStarted = true;

                lobbyNetworkService.SetIsHost(true);
                presentationServices.LobbyUI.SetIsHost(true);
                adminUI.SetIsHost(true);

                networkServices.AdminService.SetIsHost(true);

                presentationServices.LobbyUI.SetConnected(true);

                networkServices.SwitchToHost(applicationServices.LobbyManager);
            }
            else
            {
                Debug.Log("[Bootstrap] StartServer solicitado pero serverStarted ya es true; ignorando");
            }
        };

        presentationServices.LobbyUI.OnJoinLobby += async (ip, port) =>
        {
            Debug.Log($"[Bootstrap] Unirse a Lobby {ip}:{port}");

            if (!clientConnected)
            {
                await networkServices.Client.ConnectToServer(ip, port);
                clientConnected = true;

                lobbyNetworkService.SetIsHost(false);
                presentationServices.LobbyUI.SetIsHost(false);
                adminUI.SetIsHost(false);

                networkServices.AdminService.SetIsHost(false);

                presentationServices.LobbyUI.SetConnected(true);

                networkServices.SwitchToClient(applicationServices.LobbyManager);
            }
            else
            {
                Debug.Log("[Bootstrap] ConnectToServer solicitado pero clientConnected ya es true; ignorando");
            }
        }; ;

        presentationServices.LobbyUI.OnStartGame += async () =>
        {
            Debug.Log("[Bootstrap] Host solicitó START_GAME desde UI");
            await lobbyNetworkService.StartGame();
        };

        presentationServices.LobbyUI.OnLeaveLobby += async () =>
        {
            Debug.Log("[Bootstrap] Usuario solicitó abandonar el lobby (voluntario)");

            await lobbyNetworkService.LeaveLobby();

            presentationServices?.LobbyUI?.ResetAllToMain();

            adminUI.ClearAll();

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


        presentationServices.LobbyUI.OnShutdownServer += async () =>
        {
            Debug.Log("[Bootstrap] Host solicitó apagar el servidor desde la UI");

            await lobbyNetworkService.LeaveLobby();

            networkServices.Server?.Disconnect();

            presentationServices?.LobbyUI?.ResetAllToMain();

            presentationServices.LobbyUI.SetConnected(false);
            presentationServices.LobbyUI.SetIsHost(false);
            adminUI.SetIsHost(false);
            adminUI.ClearAll();

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

        // ==================== APPLICATION -> PRESENTATION ====================

        applicationServices.LobbyManager.OnPlayerJoined += (player) =>
        {
            Debug.Log($"[Bootstrap] Player Joined: {player.Id}");

            presentationServices.SpawnUI.OnPlayerAssigned(player.Id, player.PlayerType);

            adminUI.AddPlayerEntry(player.Id);
        };

        applicationServices.LobbyManager.OnPlayerLeft += (playerId) =>
        {
            Debug.Log($"[Bootstrap] Player Left: {playerId}");

            presentationServices.SpawnUI.HandlePlayerDisconnected(playerId);

            adminUI.RemovePlayerEntry(playerId);
        };

        // ==================== ADMIN UI ====================

        adminUI.SetIsHost(false);

        adminUI.OnKickRequested += async (targetId) =>
        {
            Debug.Log($"[Bootstrap] Kick solicitado para player {targetId}");

            if (networkServices.AdminService != null)
            {
                await networkServices.AdminService.KickPlayer(targetId);
            }
        };

        Debug.Log("[GameBootstrap] Capas conectadas");
    }

    private async Task StartHostFlow()
    {
        await networkServices.Server.StartServer(presentationServices.LobbyUI.Port);
        serverStarted = true;

        lobbyNetworkService.SetIsHost(true);
        presentationServices.LobbyUI.SetIsHost(true);
        adminUI.SetIsHost(true);

        networkServices.AdminService.SetIsHost(true);

        presentationServices.LobbyUI.SetConnected(true);

        networkServices.SwitchToHost(applicationServices.LobbyManager);
    }

    private async Task StartClientFlow()
    {
        await networkServices.Client.ConnectToServer(lobbyUI.IpAddress, lobbyUI.Port);
        clientConnected = true;

        lobbyNetworkService.SetIsHost(false);
        presentationServices.LobbyUI.SetIsHost(false);
        adminUI.SetIsHost(false);

        networkServices.AdminService.SetIsHost(false);

        presentationServices.LobbyUI.SetConnected(true);

        networkServices.SwitchToClient(applicationServices.LobbyManager);
    }

}