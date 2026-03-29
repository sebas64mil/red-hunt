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
            Debug.Log($"[ConnectionManager] Cliente {endpoint} ya registrado con id {clients[endpoint].ClientId}");
            return clients[endpoint].ClientId;
        }

        if (clients.Count >= maxClients)
        {
            Debug.LogWarning($"[ConnectionManager] Límite de clientes alcanzado ({maxClients}). Rechazando {endpoint}");
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

        Debug.Log($"[ConnectionManager] Nuevo cliente {endpoint} -> id {id}. Total clientes: {clients.Count}");
        return clients[endpoint].ClientId;
    }
    public void RemoveClient(IPEndPoint endpoint)
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
        Debug.Log("[ConnectionManager] Clear: conexiones reseteadas");
    }
}