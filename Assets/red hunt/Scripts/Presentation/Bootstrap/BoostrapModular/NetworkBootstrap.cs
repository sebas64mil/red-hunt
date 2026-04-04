using System;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkBootstrap : MonoBehaviour
{
    [Header("Network (autónomo)")]
    [SerializeField] private Server server;
    [SerializeField] private Client client;

    public NetworkServices Services { get; private set; }

    // Eventos de la capa Network
    public event Action OnClientDisconnected;
    public event Action<int> OnPlayerIdAssigned;
    public event Action<int> OnLocalJoinAccepted;

    private LobbyNetworkService lobbyNetworkService;


    public void Init(ApplicationServices appServices, Server serverOverride = null, Client clientOverride = null, LobbyNetworkService lobbyNetwork = null)
    {
        if (Services != null) return;

        // utilizar overrides si se proporcionan (esto permite que ModularLobbyBootstrap pase sus referencias)
        if (serverOverride != null) server = serverOverride;
        if (clientOverride != null) client = clientOverride;

        lobbyNetworkService = lobbyNetwork ?? GetComponent<LobbyNetworkService>() ?? gameObject.AddComponent<LobbyNetworkService>();

        Services = new NetworkInstaller()
            .Install(server, client, appServices.LobbyManager, lobbyNetworkService, false);

        // Wire interno -> reexponer mediante eventos públicos
        Services.Client.OnDisconnected += HandleClientDisconnected;
        Services.ClientState.OnPlayerIdAssigned += HandlePlayerIdAssigned;

        lobbyNetworkService.OnLocalJoinAccepted += HandleLocalJoinAccepted;

        // ⭐ NUEVO: Registrar LobbyNetworkService en PacketDispatcher para manejar paquetes del lobby
        RegisterLobbyPacketHandlers();

        Debug.Log("[NetworkBootstrap] NetworkServices inicializados y eventos vinculados.");
    }

    // ⭐ NUEVO: Método para registrar handlers
    private void RegisterLobbyPacketHandlers()
    {
        if (Services?.Dispatcher == null || lobbyNetworkService == null)
        {
            Debug.LogWarning("[NetworkBootstrap] No se puede registrar handlers: Dispatcher o LobbyNetworkService es NULL");
            return;
        }

        var dispatcher = Services.Dispatcher;

        // Registrar todos los tipos de paquetes del lobby
        dispatcher.Register("PLAYER", (json, sender) => lobbyNetworkService.HandlePacketReceived(json));
        dispatcher.Register("PLAYER_READY", (json, sender) => lobbyNetworkService.HandlePacketReceived(json));
        dispatcher.Register("REMOVE_PLAYER", (json, sender) => lobbyNetworkService.HandlePacketReceived(json));
        dispatcher.Register("LOBBY_STATE", (json, sender) => lobbyNetworkService.HandlePacketReceived(json));
        dispatcher.Register("START_GAME", (json, sender) => lobbyNetworkService.HandlePacketReceived(json));
        dispatcher.Register("ASSIGN_REJECT", (json, sender) => lobbyNetworkService.HandlePacketReceived(json));
        dispatcher.Register("RETURN_TO_LOBBY", (json, sender) => lobbyNetworkService.HandlePacketReceived(json));  // ⭐ NUEVO

    }

    private void HandleClientDisconnected()
    {
        try
        {
            Debug.Log("[NetworkBootstrap] Cliente desconectado");
            OnClientDisconnected?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkBootstrap] Error en HandleClientDisconnected: {e.Message}");
        }
    }

    private void HandlePlayerIdAssigned(int id)
    {
        try
        {
            Debug.Log($"[NetworkBootstrap] PlayerId asignado: {id}");
            OnPlayerIdAssigned?.Invoke(id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkBootstrap] Error en HandlePlayerIdAssigned: {e.Message}");
        }
    }

    private void HandleLocalJoinAccepted(int id)
    {
        try
        {
            Debug.Log($"[NetworkBootstrap] OnLocalJoinAccepted: {id}");
            OnLocalJoinAccepted?.Invoke(id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkBootstrap] Error en HandleLocalJoinAccepted: {e.Message}");
        }
    }

    public Task<bool> ConnectToServer(string ip, int port)
    {
        if (Services == null) throw new InvalidOperationException("NetworkBootstrap no inicializado. Llama Init(...) primero.");
        return Services.Client.ConnectToServer(ip, port);
    }

    public Task StartServer(int port)
    {
        if (Services == null) throw new InvalidOperationException("NetworkBootstrap no inicializado. Llama Init(...) primero.");
        return Services.Server.StartServer(port);
    }

    public LobbyNetworkService GetLobbyNetworkService() => lobbyNetworkService;

    private void OnDestroy()
    {
        try
        {
            if (Services != null)
            {
                Services.Client.OnDisconnected -= HandleClientDisconnected;
                Services.ClientState.OnPlayerIdAssigned -= HandlePlayerIdAssigned;
            }

            if (lobbyNetworkService != null)
                lobbyNetworkService.OnLocalJoinAccepted -= HandleLocalJoinAccepted;

            Services?.Server?.Disconnect();
        }
        catch { }
    }
}