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

    /// <summary>
    /// Inicializa NetworkServices; si se proporcionan server/client, los usa (permite que otro bootstrap pase las instancias).
    /// Idempotente.
    /// </summary>
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

        Debug.Log("[NetworkBootstrap] NetworkServices inicializados y eventos vinculados.");
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