using System.Net;
using System.Threading.Tasks;

public interface IServer : IGameConnection
{
    public bool isServerRunning { get; }
    public Task StartServer(int port);

    Task SendToClientAsync(string message, IPEndPoint client);

}