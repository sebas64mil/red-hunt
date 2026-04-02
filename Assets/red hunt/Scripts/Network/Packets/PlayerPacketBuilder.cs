using System;

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

    public string GetPacketType(string json)
    {
        if (string.IsNullOrEmpty(json)) return string.Empty;

        var basePacket = serializer.Deserialize<BasePacket>(json);
        return basePacket?.type ?? string.Empty;
    }
}