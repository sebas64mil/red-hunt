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
                Debug.LogWarning("[AdminPacketHandler] Paquete admin inv�lido");
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
                    Debug.LogWarning($"[AdminPacketHandler] Paquete admin desconocido: {basePkt.type}");
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminPacketHandler] Error procesando paquete admin: {e.Message}");
        }
    }

    private void HandleKick(string json, IPEndPoint sender)
    {
        try
        {
            var pkt = packetBuilder.Serializer.Deserialize<KickPacket>(json);
            if (pkt == null)
            {
                Debug.LogWarning("[AdminPacketHandler] KickPacket inv�lido");
                return;
            }

            Debug.Log($"[AdminPacketHandler] ADMIN_KICK recibido target:{pkt.targetId}");

            if (adminService != null && adminService.IsHost)
            {
                Debug.LogWarning("[AdminPacketHandler] Host ignorando ADMIN_KICK desde red");
                return;
            }

             ApplyKickLocally(pkt.targetId);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminPacketHandler] Error en HandleKick: {e.Message}");
        }
    }

    private void HandlePause(string json, IPEndPoint sender)
    {
        try
        {
            var pkt = packetBuilder.Serializer.Deserialize<PausePacket>(json);
            if (pkt == null)
            {
                Debug.LogWarning("[AdminPacketHandler] PausePacket inválido");
                return;
            }

            Debug.Log($"[AdminPacketHandler] ADMIN_PAUSE recibido: {pkt.isPaused}");

            // Aplicar pausa localmente
            GameManager.SetPause(pkt.isPaused);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminPacketHandler] Error en HandlePause: {e.Message}");
        }
    }

    public async Task<bool> ExecuteKick(int targetId)
    {
        if (adminService == null)
        {
            Debug.LogWarning("[AdminPacketHandler] AdminService no inicializado");
            return false;
        }

        if (!adminService.IsHost)
        {
            Debug.LogWarning("[AdminPacketHandler] Solo el host puede iniciar un kick");
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
                Debug.Log("[AdminPacketHandler] Soy el objetivo del kick -> limpiando estado local y desconectando cliente local");

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
                    Debug.LogWarning($"[AdminPacketHandler] Error limpiando estado local antes del kick: {e.Message}");
                }

                client?.Disconnect();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminPacketHandler] Error aplicando kick local: {e.Message}");
        }
    }
}