using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [Header("Network")]
    [SerializeField] private Server server;
    [SerializeField] private Client client;

    [Header("Presentation")]
    [SerializeField] private LobbyUI lobbyUI;
    [SerializeField] private SpawnUI spawnUI;

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
            Debug.Log("[GameBootstrap] Fase 1: Instalando Application...");
            var applicationInstaller = new ApplicationInstaller();
            applicationServices = applicationInstaller.Install();

            // ==================== FASE 2: NETWORK ====================
            Debug.Log("[GameBootstrap] Fase 2: Instalando Network...");
            networkServices = new NetworkInstaller()
                .Install(server, client, applicationServices.LobbyManager);

            // ==================== FASE 3: PRESENTATION ====================
            Debug.Log("[GameBootstrap] Fase 3: Instalando Presentation...");
            presentationServices = new PresentationInstaller()
                .Install(lobbyUI, spawnUI);

            // ==================== FASE 4: LOBBY NETWORK SERVICE ====================
            Debug.Log("[GameBootstrap] Fase 4: Inicializando LobbyNetworkService...");
            lobbyNetworkService = gameObject.AddComponent<LobbyNetworkService>();
            lobbyNetworkService.Init(
                applicationServices.LobbyManager,
                networkServices.BroadcastService,
                networkServices.Builder,
                false
            );

            // ==================== FASE 5: CONEXIÓN DE CAPAS ====================
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
        // UI -> NETWORK
        presentationServices.LobbyUI.OnCreateLobby += async (ip, port) =>
        {
            Debug.Log($"[Bootstrap] Crear Lobby {ip}:{port}");

            await networkServices.Server.StartServer(port);

            lobbyNetworkService.SetIsHost(true);
            lobbyNetworkService.JoinLobby();
        };

        presentationServices.LobbyUI.OnJoinLobby += async (ip, port) =>
        {
            Debug.Log($"[Bootstrap] Unirse a Lobby {ip}:{port}");

            await networkServices.Client.ConnectToServer(ip, port);

            lobbyNetworkService.SetIsHost(false);
            lobbyNetworkService.JoinLobby();
        };

        // APPLICATION -> PRESENTATION
        applicationServices.LobbyManager.OnPlayerJoined += (player) =>
        {
            Debug.Log($"[Bootstrap] Player Joined: {player.Id}");
            presentationServices.SpawnUI.OnPlayerAssigned(player.Id, player.PlayerType);
        };

        applicationServices.LobbyManager.OnPlayerLeft += (playerId) =>
        {
            Debug.Log($"[Bootstrap] Player Left: {playerId}");
            presentationServices.SpawnUI.HandlePlayerDisconnected(playerId);
        };

        Debug.Log("[GameBootstrap] Capas conectadas");
    }
}