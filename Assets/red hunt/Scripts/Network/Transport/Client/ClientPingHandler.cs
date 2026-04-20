using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

public class ClientPingHandler
{
    private readonly IClient client;
    private readonly ISerializer serializer;
    private readonly AdminPacketBuilder adminBuilder;
    private readonly ClientState clientState;

    public ClientPingHandler(
        IClient client,
        ISerializer serializer,
        AdminPacketBuilder adminBuilder,
        ClientState clientState)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.adminBuilder = adminBuilder ?? throw new ArgumentNullException(nameof(adminBuilder));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
    }

    public async void HandlePing(string json, IPEndPoint sender)
    {
        try
        {
            var pingPacket = serializer.Deserialize<PingPacket>(json);
            if (pingPacket == null)
            {
                Debug.LogWarning("[ClientPingHandler] Invalid PingPacket");
                return;
            }

            var pongJson = adminBuilder.CreatePong(
                pingPacket.pingTimestamp,
                clientState.PlayerId
            );

            await client.SendMessageAsync(pongJson);

        }
        catch (Exception e)
        {
            Debug.LogError($"[ClientPingHandler] Error processing PING: {e.Message}");
        }
    }
}
