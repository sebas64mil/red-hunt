using System.Threading.Tasks;

public interface IClient : IGameConnection
{
    public bool isConnected { get; }
    public Task<bool> ConnectToServer(string ip, int port);
    Task SendMessageAsync(string message);
}

