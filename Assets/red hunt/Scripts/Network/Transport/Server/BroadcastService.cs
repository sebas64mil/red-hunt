using System.Threading.Tasks;


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
}