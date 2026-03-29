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
            Debug.LogWarning("[AdminNetworkService] Solo el host puede ejecutar KickPlayer");
            return false;
        }

        if (connectionManager == null)
        {
            Debug.LogWarning("[AdminNetworkService] ConnectionManager no inicializado");
            return false;
        }

        if (!connectionManager.TryGetEndpointById(targetId, out var endpoint))
        {
            Debug.LogWarning($"[AdminNetworkService] No se encontró endpoint para id {targetId}");
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

            Debug.Log($"[AdminNetworkService] Jugador {targetId} expulsado correctamente");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminNetworkService] Error expulsando jugador {targetId}: {e.Message}");
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
            Debug.LogWarning("[AdminNetworkService] Paquete ADMIN_ recibido en servidor: validar autoridad antes de actuar");
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