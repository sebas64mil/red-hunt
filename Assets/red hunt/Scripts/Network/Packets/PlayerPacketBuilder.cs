using System;
using System.Collections.Generic;
using System.Linq;

public class PlayerPacketBuilder
{
    private readonly ISerializer serializer;

    public PlayerPacketBuilder(ISerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public ISerializer Serializer => serializer;

    public string CreateMovePacket(int playerId, UnityEngine.Transform transform, UnityEngine.Vector3 velocity, bool isJumping)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var packet = new MovePacket();
        packet.SetFromTransform(playerId, transform, velocity, isJumping, timestamp);

        return serializer.Serialize(packet);
    }

    public MovePacket DeserializeMovePacket(string json)
    {
        return serializer.Deserialize<MovePacket>(json);
    }

    public string CreatePlayerStateSnapshot(Dictionary<int, (UnityEngine.Transform transform, UnityEngine.Vector3 velocity, bool isJumping)> playersData)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var snapshot = new PlayerStateSnapshot { timestamp = timestamp };

        foreach (var kvp in playersData)
        {
            int playerId = kvp.Key;
            var (transform, velocity, isJumping) = kvp.Value;

            var playerState = new PlayerStateData
            {
                playerId = playerId,
                posX = transform.position.x,
                posY = transform.position.y,
                posZ = transform.position.z,
                rotX = transform.rotation.x,
                rotY = transform.rotation.y,
                rotZ = transform.rotation.z,
                rotW = transform.rotation.w,
                velocityX = velocity.x,
                velocityY = velocity.y,
                velocityZ = velocity.z,
                isJumping = isJumping
            };

            snapshot.players.Add(playerState);
        }

        return serializer.Serialize(snapshot);
    }

    public PlayerStateSnapshot DeserializePlayerStateSnapshot(string json)
    {
        return serializer.Deserialize<PlayerStateSnapshot>(json);
    }

    public string GetPacketType(string json)
    {
        if (string.IsNullOrEmpty(json)) return string.Empty;

        var basePacket = serializer.Deserialize<BasePacket>(json);
        return basePacket?.type ?? string.Empty;
    }
}