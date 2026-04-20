using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class LatencyService : MonoBehaviour
{
    private IServer server;
    private AdminPacketBuilder adminBuilder;
    private ClientConnectionManager connectionManager;

    private float lastPingTime = 0f;
    private const float PING_INTERVAL_SECONDS = 2.0f;

    public void Init(
        IServer server,
        AdminPacketBuilder adminBuilder,
        ClientConnectionManager connectionManager)
    {
        this.server = server ?? throw new ArgumentNullException(nameof(server));
        this.adminBuilder = adminBuilder ?? throw new ArgumentNullException(nameof(adminBuilder));
        this.connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));

        DontDestroyOnLoad(gameObject);

        lastPingTime = Time.time;
    }

    private void Update()
    {
        if (server == null || !server.isServerRunning) return;

        if (Time.time - lastPingTime >= PING_INTERVAL_SECONDS)
        {
            SendPingsToAllClients();
            lastPingTime = Time.time;
        }
    }

    private async void SendPingsToAllClients()
    {
        try
        {
            var clients = connectionManager.GetAllClients();
            if (clients.Count == 0) return;

            foreach (var endpoint in clients)
            {
                int clientId = connectionManager.GetClientId(endpoint);
                var pingPacket = adminBuilder.CreatePing(clientId);

                await server.SendToClientAsync(pingPacket, endpoint);

                var connection = connectionManager.GetClientConnection(endpoint);
                if (connection != null)
                {
                    connection.LastPingTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
            }

        }
        catch (Exception e)
        {
            Debug.LogError($"[LatencyService] Error sending PINGs: {e.Message}");
        }
    }

    public int GetClientLatency(int clientId)
    {
        if (connectionManager.TryGetEndpointById(clientId, out var endpoint))
        {
            var connection = connectionManager.GetClientConnection(endpoint);
            if (connection != null)
            {
                return connection.LastPingMs;
            }
        }
        return 0;
    }
}
