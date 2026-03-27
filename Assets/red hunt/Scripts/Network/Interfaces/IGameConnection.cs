using System;
using System.Net;
using System.Threading.Tasks;
public interface IGameConnection
{
    event Action<string, IPEndPoint> OnMessageReceived;
    event Action OnDisconnected;

    void Disconnect();
}
