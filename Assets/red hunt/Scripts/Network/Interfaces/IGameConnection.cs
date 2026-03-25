using System;
using System.Net;
using System.Threading.Tasks;
public interface IGameConnection
{
    event Action<string, IPEndPoint> OnMessageReceived;
    event Action OnConnected;
    event Action OnDisconnected;

    Task SendMessageAsync(string message);
    void Disconnect();
}
