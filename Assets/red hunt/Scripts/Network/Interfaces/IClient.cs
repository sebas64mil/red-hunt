using System.Threading.Tasks;

public interface IClient : IGameConnection
{
    public bool isConnected { get; }
    public Task ConnectToServer(string ip, int port);
}

