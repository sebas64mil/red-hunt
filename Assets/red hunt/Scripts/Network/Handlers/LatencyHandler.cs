using System;
using System.Net;
using UnityEngine;

public class LatencyHandler
{
    private readonly ClientConnectionManager connectionManager;
    private readonly ISerializer serializer;

    public LatencyHandler(ClientConnectionManager connectionManager, ISerializer serializer)
    {
        this.connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public void HandlePong(string json, IPEndPoint sender)
    {
        try
        {
            var pongPacket = serializer.Deserialize<PongPacket>(json);
            if (pongPacket == null)
            {
                Debug.LogWarning("[LatencyHandler] Invalid PongPacket");
                return;
            }

            long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int latencyMs = (int)(currentTime - pongPacket.pingTimestamp);

            var connection = connectionManager.GetClientConnection(sender);
            if (connection != null)
            {
                connection.LastPingMs = latencyMs;
                connection.PingCount++;
            }
            else
            {
                Debug.LogWarning($"[LatencyHandler] Connection not found for {sender}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LatencyHandler] Error processing PONG: {e.Message}");
        }
    }
}
