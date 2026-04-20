using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

public class AdminPacketHandler
{
    private readonly PacketBuilder packetBuilder;
    private readonly AdminNetworkService adminService;
    private readonly ClientState clientState;
    private readonly LobbyManager lobbyManager;
    private readonly IClient client;
    private readonly SpawnManager spawnManager;

    public AdminPacketHandler(
        PacketBuilder packetBuilder,
        AdminNetworkService adminService,
        ClientState clientState,
        LobbyManager lobbyManager,
        IClient client,
        SpawnManager spawnManager)
    {
        this.packetBuilder = packetBuilder;
        this.adminService = adminService;
        this.clientState = clientState;
        this.lobbyManager = lobbyManager;
        this.client = client;
        this.spawnManager = spawnManager;
    }

    public void Handle(string json, IPEndPoint sender)
    {
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var basePkt = packetBuilder.Serializer.Deserialize<BasePacket>(json);
            if (basePkt == null || string.IsNullOrEmpty(basePkt.type))
            {
                Debug.LogWarning("[AdminPacketHandler] Invalid admin packet");
                return;
            }

            switch (basePkt.type)
            {
                case "ADMIN_KICK":
                    HandleKick(json, sender);
                    break;

                case "ADMIN_PAUSE":
                    HandlePause(json, sender);
                    break;

                default:
                    Debug.LogWarning($"[AdminPacketHandler] Unknown admin packet: {basePkt.type}");
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminPacketHandler] Error processing admin packet: {e.Message}");
        }
    }

    private void HandleKick(string json, IPEndPoint sender)
    {
        try
        {
            var pkt = packetBuilder.Serializer.Deserialize<KickPacket>(json);
            if (pkt == null)
            {
                Debug.LogWarning("[AdminPacketHandler] Invalid KickPacket");
                return;
            }

            if (adminService != null && adminService.IsHost)
            {
                Debug.LogWarning("[AdminPacketHandler] Host ignoring ADMIN_KICK from network");
                return;
            }

            ApplyKickLocally(pkt.targetId);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminPacketHandler] Error in HandleKick: {e.Message}");
        }
    }

    private void HandlePause(string json, IPEndPoint sender)
    {
        try
        {
            var pkt = packetBuilder.Serializer.Deserialize<PausePacket>(json);
            if (pkt == null)
            {
                Debug.LogWarning("[AdminPacketHandler] Invalid PausePacket");
                return;
            }

            GameManager.SetPause(pkt.isPaused);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminPacketHandler] Error in HandlePause: {e.Message}");
        }
    }

    public async Task<bool> ExecuteKick(int targetId)
    {
        if (adminService == null)
        {
            Debug.LogWarning("[AdminPacketHandler] AdminService not initialized");
            return false;
        }

        if (!adminService.IsHost)
        {
            Debug.LogWarning("[AdminPacketHandler] Only host can initiate a kick");
            return false;
        }

        return await adminService.KickPlayer(targetId);
    }

    public void ApplyKickLocally(int targetId)
    {
        try
        {
            int myId = clientState != null ? clientState.PlayerId : -1;

            if (myId == targetId)
            {

                try
                {
                    var boot = ModularLobbyBootstrap.Instance;
                    if (boot != null)
                    {
                        var presentation = boot.GetComponent<PresentationBootstrap>();
                        if (presentation != null)
                        {
                            presentation.SetReturningFromGameScene(true);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[AdminPacketHandler] Error marking return to lobby: {e.Message}");
                }

                try
                {
                    GameManager.SetCursorVisible(true);
                    GameManager.ChangeScene("Lobby");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[AdminPacketHandler] Error changing to Lobby scene after kick: {e.Message}");
                }

                try
                {
                    var players = lobbyManager?.GetAllPlayers()?.ToList();
                    if (players != null)
                    {
                        foreach (var p in players)
                        {
                            lobbyManager.RemovePlayerRemote(p.Id);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[AdminPacketHandler] Error cleaning local state before kick: {e.Message}");
                }

                client?.Disconnect();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminPacketHandler] Error applying local kick: {e.Message}");
        }
    }
}