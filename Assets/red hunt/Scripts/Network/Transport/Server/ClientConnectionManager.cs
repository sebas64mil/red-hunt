using System.Collections.Generic;
using System.Net;

public class ClientConnectionManager
{
    private int nextId = 1;

    private readonly Dictionary<IPEndPoint, ClientConnection> clients = new();

    public int AddClient(IPEndPoint endpoint)
    {
        if (!clients.ContainsKey(endpoint))
        {
            int id = nextId++;

            var connection = new ClientConnection(endpoint, id); 
            clients[endpoint] = connection;
        }

        return clients[endpoint].ClientId;
    }

    public void RemoveClient(IPEndPoint endpoint)
    {
        clients.Remove(endpoint);
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
}