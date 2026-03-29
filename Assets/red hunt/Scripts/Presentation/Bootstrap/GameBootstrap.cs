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
            Debug.Log("[Bootstrap] Cliente desconectado - ocultando botón Leave");
            presentationServices?.LobbyUI?.SetConnected(false);
        };

        // ==================== UI -> NETWORK ====================

        presentationServices.LobbyUI.OnCreateLobby += async (ip, port) =>
        {
            Debug.Log($"[Bootstrap] Crear Lobby {ip}:{port}");

            await networkServices.Server.StartServer(port);

            lobbyNetworkService.SetIsHost(true);
            presentationServices.LobbyUI.SetIsHost(true);
            adminUI.SetIsHost(true);

            networkServices.AdminService.SetIsHost(true);

            presentationServices.LobbyUI.SetConnected(true);

            networkServices.SwitchToHost(applicationServices.LobbyManager);

            lobbyNetworkService.JoinLobby();
        };

        presentationServices.LobbyUI.OnJoinLobby += async (ip, port) =>
        {
            Debug.Log($"[Bootstrap] Unirse a Lobby {ip}:{port}");

            await networkServices.Client.ConnectToServer(ip, port);

            lobbyNetworkService.SetIsHost(false);
            presentationServices.LobbyUI.SetIsHost(false);
            adminUI.SetIsHost(false);

            networkServices.AdminService.SetIsHost(false);

            presentationServices.LobbyUI.SetConnected(true);

            networkServices.SwitchToClient(applicationServices.LobbyManager);

            lobbyNetworkService.JoinLobby();
        };

        presentationServices.LobbyUI.OnLeaveLobby += async () =>
        {
            await lobbyNetworkService.LeaveLobby();

            presentationServices.LobbyUI.SetConnected(false);
            adminUI.ClearAll();
        };

        presentationServices.LobbyUI.OnShutdownServer += async () =>
        {
            Debug.Log("[Bootstrap] Host solicitó apagar el servidor desde la UI");

            await lobbyNetworkService.LeaveLobby();

            networkServices.Server?.Disconnect();

            presentationServices.LobbyUI.SetConnected(false);
            presentationServices.LobbyUI.SetIsHost(false);
            adminUI.SetIsHost(false);
            adminUI.ClearAll();
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
}