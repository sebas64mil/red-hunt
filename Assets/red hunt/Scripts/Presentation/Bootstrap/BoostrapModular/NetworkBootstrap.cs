using System;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkBootstrap : MonoBehaviour
{
    [Header("Network (autonomous)")]
    [SerializeField] private Server server;
    [SerializeField] private Client client;

    public NetworkServices Services { get; private set; }

    public event Action OnClientDisconnected;
    public event Action<int> OnPlayerIdAssigned;
    public event Action<int> OnLocalJoinAccepted;

    private LobbyNetworkService lobbyNetworkService;
    private GameNetworkService gameNetworkService;

    public void Init(ApplicationServices appServices, bool isHost, Server serverOverride = null, Client clientOverride = null, LobbyNetworkService lobbyNetwork = null)
    {
        if (Services != null) return;

        if (serverOverride != null) server = serverOverride;
        if (clientOverride != null) client = clientOverride;

        lobbyNetworkService = lobbyNetwork ?? GetComponent<LobbyNetworkService>() ?? gameObject.AddComponent<LobbyNetworkService>();
        gameNetworkService = GetComponent<GameNetworkService>() ?? gameObject.AddComponent<GameNetworkService>();

        Services = new NetworkInstaller()
            .Install(server, client, appServices.LobbyManager, lobbyNetworkService, gameNetworkService, isHost);

        Services.Client.OnDisconnected += HandleClientDisconnected;
        Services.ClientState.OnPlayerIdAssigned += HandlePlayerIdAssigned;

        lobbyNetworkService.OnLocalJoinAccepted += HandleLocalJoinAccepted;
    }

    private void HandleClientDisconnected()
    {
        try
        {
            OnClientDisconnected?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkBootstrap] Error in HandleClientDisconnected: {e.Message}");
        }
    }

    private void HandlePlayerIdAssigned(int id)
    {
        try
        {
            OnPlayerIdAssigned?.Invoke(id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkBootstrap] Error in HandlePlayerIdAssigned: {e.Message}");
        }
    }

    private void HandleLocalJoinAccepted(int id)
    {
        try
        {
            OnLocalJoinAccepted?.Invoke(id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkBootstrap] Error in HandleLocalJoinAccepted: {e.Message}");
        }
    }

    public Task<bool> ConnectToServer(string ip, int port)
    {
        if (Services == null) throw new InvalidOperationException("NetworkBootstrap not initialized. Call Init(...) first.");
        return Services.Client.ConnectToServer(ip, port);
    }

    public Task StartServer(int port)
    {
        if (Services == null) throw new InvalidOperationException("NetworkBootstrap not initialized. Call Init(...) first.");
        return Services.Server.StartServer(port);
    }

    public LobbyNetworkService GetLobbyNetworkService() => lobbyNetworkService;

    public GameNetworkService GetGameNetworkService() => gameNetworkService;

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