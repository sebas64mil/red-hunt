using System.Threading.Tasks;
using UnityEngine; 
using System;
using System.Net;

public class BroadcastService
{
    private readonly IServer server;
    private readonly ClientConnectionManager manager;

    public BroadcastService(IServer server, ClientConnectionManager manager)
    {
        this.server = server;
        this.manager = manager;
    }

    public async Task SendToAll(string message)
    {
        var clients = manager.GetAllClients();

        foreach (var client in clients)
        {
            await server.SendToClientAsync(message, client);
        }
    }

    public async Task SendToAllExcept(string data, IPEndPoint excludeSender)
    {
        try
        {
            var clients = manager.GetAllClients();
            foreach (var client in clients)
            {
                if (!client.Equals(excludeSender))
                {
                    await server.SendToClientAsync(data, client);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BroadcastService] Error en SendToAllExcept: {e.Message}");
        }
    }
}