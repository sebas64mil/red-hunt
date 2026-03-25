using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Server : MonoBehaviour, IServer
{
    private UdpClient udpServer;

    private PacketDispatcher dispatcher;
    private ISerializer serializer;

    private IPEndPoint hostClient;

    private List<IPEndPoint> clients = new List<IPEndPoint>();

    public event Action<string, IPEndPoint> OnMessageReceived;

    public event Action OnConnected;
    public event Action OnDisconnected;

    private Dictionary<IPEndPoint, int> clientIds = new Dictionary<IPEndPoint, int>();
    private int nextId = 2;

    public bool isServerRunning { get; private set; }



    public void Init(PacketDispatcher dispatcher, ISerializer serializer)
    {
        this.dispatcher = dispatcher;
        this.serializer = serializer;

        OnMessageReceived += (msg, sender) =>
        {
            dispatcher.Dispatch(msg, sender);
        };
    }

    public Task StartServer(int port)
    {
        udpServer = new UdpClient(port);
        Debug.Log("[Server] Server started. Waiting for messages...");
        isServerRunning = true;

        _ = ReceiveLoop();
        return Task.CompletedTask;
    }

    private async Task ReceiveLoop()
    {
        try
        {
            while (isServerRunning)
            {
                UdpReceiveResult result = await udpServer.ReceiveAsync();
                string message = Encoding.UTF8.GetString(result.Buffer);

                IPEndPoint clientEndPoint = result.RemoteEndPoint;

                if (message == "CONNECT")
                {
                    if (!clients.Contains(clientEndPoint))
                    {
                        clients.Add(clientEndPoint);

                        int assignedId;

                        if (hostClient == null)
                        {
                            hostClient = clientEndPoint;
                            assignedId = 1;

                            Debug.Log("[Server] HOST assigned: " + clientEndPoint);
                        }
                        else
                        {
                            assignedId = nextId++;
                        }

                        clientIds[clientEndPoint] = assignedId;

                        Debug.Log($"[Server] Client connected: {clientEndPoint} | ID: {assignedId}");

                        OnConnected?.Invoke();

                        var playerPacket = new PlayerPacket
                        {
                            type = "PLAYER",
                            id = assignedId,
                            playerType = assignedId == 1 ? "KILLER" : "ESCAPIST"
                        };

                        string json = serializer.Serialize(playerPacket);

                        await SendToClientAsync(json, clientEndPoint);
                    }

                    await SendToClientAsync("CONNECTED", clientEndPoint);
                    continue;
                }

                Debug.Log($"[Server] Received from {clientEndPoint}: {message}");

                OnMessageReceived?.Invoke(message, clientEndPoint);
            }
        }
        finally
        {
            Disconnect();
        }
    }

    public async Task SendMessageAsync(string message)
    {
        if (!isServerRunning) return;

        byte[] data = Encoding.UTF8.GetBytes(message);

        foreach (var client in clients)
        {
            await udpServer.SendAsync(data, data.Length, client);
        }

        Debug.Log("[Server] Sent to ALL: " + message);
    }

    public async Task SendToClientAsync(string message, IPEndPoint client)
    {
        if (!isServerRunning) return;

        byte[] data = Encoding.UTF8.GetBytes(message);
        await udpServer.SendAsync(data, data.Length, client);

        Debug.Log("[Server] Sent to " + client + ": " + message);
    }

    public void RemoveClient(IPEndPoint client)
    {
        if (clients.Contains(client))
        {
            clients.Remove(client);
            Debug.Log("[Server] Client removed: " + client);
        }
    }

    public void Disconnect()
    {
        if (!isServerRunning)
        {
            Debug.Log("[Server] The server is not running");
            return;
        }

        isServerRunning = false;

        udpServer?.Close();
        udpServer?.Dispose();
        udpServer = null;

        clients.Clear(); 

        Debug.Log("[Server] Disconnected");
        OnDisconnected?.Invoke();
    }

    private async void OnDestroy()
    {
        Disconnect();
        await Task.Delay(100);
    }
}