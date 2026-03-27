using UnityEngine;
using System.Threading.Tasks;

public class LobbyNetworkService : MonoBehaviour
{
    private LobbyManager lobbyManager;
    private BroadcastService broadcastService;
    private PacketBuilder packetBuilder;

    private bool isHost;

    public void Init(LobbyManager lobbyManager, BroadcastService broadcastService, PacketBuilder packetBuilder, bool isHost)
    {
        this.lobbyManager = lobbyManager;
        this.broadcastService = broadcastService;
        this.packetBuilder = packetBuilder;
        this.isHost = isHost;

        lobbyManager.OnPlayerJoined += async (player) => await HandlePlayerJoined(player);
    }

    public void SetIsHost(bool value)
    {
        this.isHost = value;
    }

    public void JoinLobby()
    {
        string playerType = isHost ? PlayerType.Killer.ToString() : PlayerType.Escapist.ToString();
        var command = new JoinLobbyCommand(playerType);
        lobbyManager.ExecuteCommand(command);
    }

    private async Task HandlePlayerJoined(PlayerSession player)
    {
        if (!isHost) return;

        Debug.Log($"[LobbyNetworkService] Broadcast Player {player.Id} - {player.PlayerType}");

        string packetJson = packetBuilder.CreatePlayer(player.Id, player.PlayerType);
        await broadcastService.SendToAll(packetJson);
    }

    public void AddRemotePlayer(int id, PlayerType type)
    {
        lobbyManager.AddPlayerRemote(id, type.ToString());
    }

    public void SetPlayerReady(int id)
    {
        lobbyManager.SetPlayerReady(id);
    }
}