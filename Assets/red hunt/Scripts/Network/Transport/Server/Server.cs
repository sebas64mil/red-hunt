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

    private List<IPEndPoint> clients = new List<IPEndPoint>();
    private Dictionary<IPEndPoint, int> clientIds = new Dictionary<IPEndPoint, int>();

    private int nextId = 2;
    private List<int> availableIds = new List<int>(); 

    public bool isServerRunning { get; private set; }
    private const int maxClients = 4;

    public event Action<string, IPEndPoint> OnMessageReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;
    // Nuevo evento para notificar al host/local sobre jugadores que se unen
    public event Action<PlayerPacket> OnPlayerJoined;

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
                    await HandleConnect(clientEndPoint);
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

    private async Task HandleConnect(IPEndPoint clientEndPoint)
    {
        if (clients.Contains(clientEndPoint))
        {
            await SendToClientAsync("CONNECTED", clientEndPoint);
            return;
        }

        if (clients.Count >= maxClients)
        {
            Debug.LogWarning("[Server] Lobby full. Rejecting client: " + clientEndPoint);
            await SendToClientAsync("LOBBY_FULL", clientEndPoint);
            return;
        }

        RegisterClient(clientEndPoint);

        int assignedId = AssignClientId(clientEndPoint);

        Debug.Log($"[Server] Client connected: {clientEndPoint} | ID: {assignedId}");

        OnConnected?.Invoke();

        await SendPlayerPacket(clientEndPoint, assignedId);

        await SendExistingPlayers(clientEndPoint);

        // Crear paquete PLAYER para el nuevo cliente
        var newPlayerPacket = new PlayerPacket
        {
            type = "PLAYER",
            id = assignedId,
            playerType = (assignedId == 1) ? "KILLER" : "ESCAPIST"
        };

        // Notificar al host/local (si hay suscriptores) para que instancie localmente
        OnPlayerJoined?.Invoke(newPlayerPacket);

        // Notificar a los clientes existentes sobre el nuevo jugador (excepto el que acaba de conectar)
        string newPlayerJson = serializer.Serialize(newPlayerPacket);
        foreach (var c in clients)
        {
            if (c.Equals(clientEndPoint)) continue;
            await SendToClientAsync(newPlayerJson, c);
        }

        await SendToClientAsync("CONNECTED", clientEndPoint);
    }

    private void RegisterClient(IPEndPoint client)
    {
        clients.Add(client);
        Debug.Log("[Server] Client registered: " + client);
    }

    private int AssignClientId(IPEndPoint client)
    {
        int assignedId;

        if (availableIds.Count > 0)
        {
            assignedId = availableIds[0];
            availableIds.RemoveAt(0);
        }
        else
        {
            assignedId = nextId++;
        }

        clientIds[client] = assignedId;
        return assignedId;
    }

    private async Task SendPlayerPacket(IPEndPoint client, int id)
    {
        var playerPacket = new PlayerPacket
        {
            type = "ASSIGN_PLAYER",
            id = id,
            playerType = "ESCAPIST"
        };

        string json = serializer.Serialize(playerPacket);

        await SendToClientAsync(json, client);
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

    private async Task SendExistingPlayers(IPEndPoint clientEndPoint)
    {
        // Ensure the host (ID 1) is sent first so newly connected clients see the host player
        var hostPacket = new PlayerPacket
        {
            type = "PLAYER",
            id = 1,
            playerType = "KILLER"
        };

        await SendToClientAsync(serializer.Serialize(hostPacket), clientEndPoint);

        foreach (var kv in clientIds)
        {
            int id = kv.Value;

            // Skip host if somehow present in clientIds to avoid duplicate
            if (id == 1) continue;

            string playerType = (id == 1) ? "KILLER" : "ESCAPIST";

            var packet = new PlayerPacket
            {
                type = "PLAYER",
                id = id,
                playerType = playerType
            };

            string json = serializer.Serialize(packet);
            await SendToClientAsync(json, clientEndPoint);
        }
    }


    public void RemoveClient(IPEndPoint client)
    {
        if (clients.Contains(client))
        {
            // Liberar ID
            if (clientIds.TryGetValue(client, out int freedId))
            {
                availableIds.Add(freedId);
            }

            clients.Remove(client);
            clientIds.Remove(client);

            Debug.Log("[Server] Client removed: " + client + " | Freed ID: " + freedId);
        }
    }

    public void Disconnect()
    {
        if (!isServerRunning)
        {
            return;
        }

        isServerRunning = false;

        udpServer?.Close();
        udpServer?.Dispose();
        udpServer = null;

        clients.Clear();
        clientIds.Clear();
        availableIds.Clear();

        Debug.Log("[Server] Disconnected");

        OnDisconnected?.Invoke();
    }

    private async void OnDestroy()
    {
        Disconnect();
        await Task.Delay(100);
    }
}