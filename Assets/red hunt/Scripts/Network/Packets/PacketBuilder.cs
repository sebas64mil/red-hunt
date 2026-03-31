using System.Collections.Generic;
using System.Linq;

public class PacketBuilder
{
    private readonly ISerializer serializer;

    public PacketBuilder(ISerializer serializer)
    {
        this.serializer = serializer;
    }
    public ISerializer Serializer => serializer;
    public string CreateAssignPlayer(int id)
    {
        var packet = new AssignPlayerPacket
        {
            type = "ASSIGN_PLAYER",
            id = id
        };
        return serializer.Serialize(packet);
    }

    public string CreatePlayer(int id, string playerType)
    {
        var packet = new PlayerPacket
        {
            type = "PLAYER",
            id = id,
            playerType = playerType
        };
        return serializer.Serialize(packet);
    }

    public string CreatePlayerReady(int id)
    {
        var packet = new PlayerReadyPacket
        {
            type = "PLAYER_READY",
            id = id
        };
        return serializer.Serialize(packet);
    }

    public string CreateLobbyState(IEnumerable<PlayerSession> players, int hostId)
    {
        var packet = new LobbyStatePacket
        {
            type = "LOBBY_STATE",
            Players = players.Select(p => new PlayerInfoLobby
            {
                Id = p.Id,
                Name = p.PlayerType.ToString(),
                IsHost = p.Id == hostId
            }).ToList()
        };
        return serializer.Serialize(packet);
    }

    public string CreateDisconnect()
    {
        var packet = new DisconnectPacket
        {
            type = "DISCONNECT"
        };

        return serializer.Serialize(packet);
    }

    public string CreateRemovePlayer(int id)
    {
        var packet = new RemovePlayerPacket
        {
            type = "REMOVE_PLAYER",
            id = id
        };

        return serializer.Serialize(packet);
    }

    public string CreateStartGame(string sceneName)
    {
        var packet = new StartGamePacket
        {
            type = "START_GAME",
            sceneName = sceneName
        };
        return serializer.Serialize(packet);
    }


    public string CreateAssignReject(int id, string reason)
    {
        var packet = new AssignRejectPacket
        {
            type = "ASSIGN_REJECT",
            id = id,
            reason = reason
        };
        return serializer.Serialize(packet);
    }


    public string GetPacketType(string json)
    {
        if (string.IsNullOrEmpty(json)) return string.Empty;

        var basePacket = serializer.Deserialize<BasePacket>(json);
        return basePacket?.type ?? string.Empty;
    }

    public LobbyStatePacket DeserializeLobbyState(string json)
    {
        return serializer.Deserialize<LobbyStatePacket>(json);
    }
}