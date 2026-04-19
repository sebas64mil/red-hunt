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

    public string CreateReturnToLobby()
    {
        var packet = new ReturnToLobbyPacket
        {
            type = "RETURN_TO_LOBBY",
            message = "Host is returning to lobby"
        };
        return serializer.Serialize(packet);
    }

    public string CreateWinGame(int winnerId, string winnerType, bool isKillerWin)
    {
        var packet = new WinGamePacket
        {
            type = "WIN_GAME",
            winnerId = winnerId,
            winnerType = winnerType,
            isKillerWin = isKillerWin
        };
        return serializer.Serialize(packet);
    }

    public string CreateHealthUpdate(int playerId, int currentHealth, int maxHealth)
    {
        var packet = new HealthUpdatePacket
        {
            type = "HEALTH_UPDATE",
            playerId = playerId,
            currentHealth = currentHealth,
            maxHealth = maxHealth
        };
        return serializer.Serialize(packet);
    }

    public string CreateEscapistPassed(int escapistId)
    {
        var packet = new EscapistPassedPacket
        {
            type = "ESCAPIST_PASSED",
            escapistId = escapistId
        };
        return serializer.Serialize(packet);
    }

    public string CreateEscapistsPassedSnapshot(IEnumerable<int> targetIds, IEnumerable<int> passedIds)
    {
        var packet = new EscapistsPassedSnapshotPacket
        {
            type = "ESCAPISTS_PASSED_SNAPSHOT",
            targetEscapistIds = targetIds?.Distinct().OrderBy(id => id).ToList() ?? new List<int>(),
            passedEscapistIds = passedIds?.Distinct().OrderBy(id => id).ToList() ?? new List<int>()
        };

        return serializer.Serialize(packet);
    }


    public string CreateEscapistClueCollected(int escapistId, string clueId)
    {
        var packet = new EscapistClueCollectedPacket
        {
            type = "ESCAPIST_CLUE_COLLECTED",
            escapistId = escapistId,
            clueId = clueId
        };

        return serializer.Serialize(packet);
    }

    public string CreateEscapistsCluesSnapshot(IEnumerable<int> targetIds, IReadOnlyDictionary<int, IReadOnlyCollection<string>> cluesByEscapist)
    {
        var packet = new EscapistsCluesSnapshotPacket
        {
            type = "ESCAPISTS_CLUES_SNAPSHOT",
            targetEscapistIds = targetIds?.Distinct().OrderBy(id => id).ToList() ?? new List<int>()
        };

        if (cluesByEscapist != null)
        {
            foreach (var id in packet.targetEscapistIds)
            {
                if (!cluesByEscapist.TryGetValue(id, out var clueIds) || clueIds == null)
                    clueIds = new List<string>();

                packet.entries.Add(new EscapistCluesEntry
                {
                    escapistId = id,
                    clueIds = clueIds.Distinct().OrderBy(x => x).ToList()
                });
            }
        }

        return serializer.Serialize(packet);
    }

    public EscapistClueCollectedPacket DeserializeEscapistClueCollected(string json)
    {
        return serializer.Deserialize<EscapistClueCollectedPacket>(json);
    }

    public EscapistsCluesSnapshotPacket DeserializeEscapistsCluesSnapshot(string json)
    {
        return serializer.Deserialize<EscapistsCluesSnapshotPacket>(json);
    }

    // ===================== EXISTENTE =====================

    public HealthUpdatePacket DeserializeHealthUpdate(string json)
    {
        return serializer.Deserialize<HealthUpdatePacket>(json);
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

    public EscapistPassedPacket DeserializeEscapistPassed(string json)
    {
        return serializer.Deserialize<EscapistPassedPacket>(json);
    }

    public EscapistsPassedSnapshotPacket DeserializeEscapistsPassedSnapshot(string json)
    {
        return serializer.Deserialize<EscapistsPassedSnapshotPacket>(json);
    }
}