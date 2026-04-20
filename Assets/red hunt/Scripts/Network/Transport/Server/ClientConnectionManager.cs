using System.Collections.Generic;
using System.Net;
using UnityEngine;
public class ClientConnectionManager
{
    private int nextId = 2;
    private readonly int maxClients;

    private readonly Dictionary<IPEndPoint, ClientConnection> clients = new();
    private readonly Queue<int> availableIds = new();


    public ClientConnectionManager(int maxClients = 3)
    {
        this.maxClients = maxClients;
    }

    public int AddClient(IPEndPoint endpoint)
    {
        if (clients.ContainsKey(endpoint))
        {
            return clients[endpoint].ClientId;
        }

        if (clients.Count >= maxClients)
        {
            Debug.LogWarning($"[ConnectionManager] Client limit reached ({maxClients}). Rejecting {endpoint}");
            return -1;
        }

        int id;
        if (availableIds.Count > 0)
        {
            id = availableIds.Dequeue();
        }
        else
        {
            id = nextId++;
        }


        var connection = new ClientConnection(endpoint, id);
        clients[endpoint] = connection;

        return clients[endpoint].ClientId;
    }
    public void RemoveClient( IPEndPoint endpoint)
    {
        if (!clients.TryGetValue(endpoint, out var connection)) return;

        clients.Remove(endpoint);

        if (connection.ClientId > 0)
            availableIds.Enqueue(connection.ClientId);
    }

    public List<IPEndPoint> GetAllClients()
    {
        return new List<IPEndPoint>(clients.Keys);
    }

    public bool Exists(IPEndPoint endpoint)
    {
        return clients.ContainsKey(endpoint);
    }

    public int GetClientId(IPEndPoint endpoint)
    {
        return clients[endpoint].ClientId;
    }

    public int GetClientCount() => clients.Count;

    public int MaxClients => maxClients;


    public void Clear()
    {
        clients.Clear();
        availableIds.Clear();
        nextId = 2;
    }

    public bool TryGetEndpointById(int clientId, out IPEndPoint endpoint)
    {
        foreach (var kvp in clients)
        {
            if (kvp.Value.ClientId == clientId)
            {
                endpoint = kvp.Key;
                return true;
            }
        }

        endpoint = null;
        return false;
    }

    public ClientConnection GetClientConnection(IPEndPoint endpoint)
    {
        if (clients.TryGetValue(endpoint, out var connection))
        {
            return connection;
        }
        return null;
    }
}