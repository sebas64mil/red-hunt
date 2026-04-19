using System;
using UnityEngine;

public class AdminPacketBuilder
{
    private readonly ISerializer serializer;

    public AdminPacketBuilder(ISerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public string CreateKick(int targetId)
    {
        var packet = new KickPacket
        {
            type = "ADMIN_KICK",
            targetId = targetId
        };

        return serializer.Serialize(packet);
    }

    public string CreatePing(int clientId)
    {
        var packet = new PingPacket
        {
            type = "ADMIN_PING",
            pingTimestamp = GetCurrentTimestampMs(),
            clientId = clientId
        };

        return serializer.Serialize(packet);
    }

    public string CreatePong(long pingTimestamp, int clientId)
    {
        var packet = new PongPacket
        {
            type = "ADMIN_PONG",
            pingTimestamp = pingTimestamp,
            clientId = clientId
        };

        return serializer.Serialize(packet);
    }

    public string CreatePause(bool isPaused)
    {
        var packet = new PausePacket
        {
            type = "ADMIN_PAUSE",
            isPaused = isPaused
        };

        return serializer.Serialize(packet);
    }

    private long GetCurrentTimestampMs()
    {
        return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}