using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

public class AdminNetworkService
{
    private readonly IServer server;
    private readonly BroadcastService broadcastService;
    private readonly PacketBuilder packetBuilder;
    private readonly AdminPacketBuilder adminBuilder;
    private readonly ClientConnectionManager connectionManager;
    private readonly LobbyManager lobbyManager;
    private readonly SpawnManager spawnManager;

    private bool isHost;
    public bool IsHost => isHost;

    public AdminNetworkService(
        IServer server,
        BroadcastService broadcastService,
        PacketBuilder packetBuilder,
        AdminPacketBuilder adminBuilder,
        ClientConnectionManager connectionManager,
        LobbyManager lobbyManager,
        SpawnManager spawnManager,
        bool isHost = false)
    {
        this.server = server;
        this.broadcastService = broadcastService;
        this.packetBuilder = packetBuilder;
        this.adminBuilder = adminBuilder;
        this.connectionManager = connectionManager;
        this.lobbyManager = lobbyManager;
        this.spawnManager = spawnManager;
        this.isHost = isHost;
    }

    public void SetIsHost(bool value)
    {
        isHost = value;
    }

    public async Task<bool> KickPlayer(int targetId)
    {
        if (!isHost)
        {
            Debug.LogWarning("[AdminNetworkService] Only host can execute KickPlayer");
            return false;
        }

        if (connectionManager == null)
        {
            Debug.LogWarning("[AdminNetworkService] ConnectionManager not initialized");
            return false;
        }

        if (!connectionManager.TryGetEndpointById(targetId, out var endpoint))
        {
            Debug.LogWarning($"[AdminNetworkService] Endpoint not found for id {targetId}");
            return false;
        }

        try
        {

            lobbyManager.RemovePlayerRemote(targetId);
            spawnManager?.RemovePlayer(targetId);

            var removePacket = packetBuilder.CreateRemovePlayer(targetId);
            await broadcastService.SendToAll(removePacket);


            connectionManager.RemoveClient(endpoint);


            var adminKick = adminBuilder.CreateKick(targetId);
            await server.SendToClientAsync(adminKick, endpoint);

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminNetworkService] Error kicking player {targetId}: {e.Message}");
            return false;
        }
    }
  


    public async Task<bool> SetGlobalPause(bool pause)
    {
        if (!isHost)
        {
            Debug.LogWarning("[AdminNetworkService] Only host can pause the game");
            return false;
        }

        try
        {
            var pausePacket = adminBuilder.CreatePause(pause);
            await broadcastService.SendToAll(pausePacket);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminNetworkService] Error sending pause: {e.Message}");
            return false;
        }
    }

    public void HandlePacketReceived(string json, IPEndPoint sender)
    {
        if (string.IsNullOrEmpty(json)) return;

        var type = packetBuilder.GetPacketType(json);
        if (string.IsNullOrEmpty(type)) return;


        if (type.StartsWith("ADMIN_"))
        {
            Debug.LogWarning("[AdminNetworkService] ADMIN_ packet received on server: validate authority before acting");
        }
    }

    public int? GetHostId()
    {
        var players = lobbyManager.GetAllPlayers().ToList();
        if (!players.Any()) return null;
        return players.Min(p => p.Id);
    }

    public bool IsSenderAuthorized(IPEndPoint sender)
    {
        if (connectionManager == null || lobbyManager == null) return false;

        try
        {
            int clientId = connectionManager.GetClientId(sender);
            var hostId = GetHostId();
            return hostId.HasValue && clientId == hostId.Value;
        }
        catch
        {
            return false;
        }
    }
}