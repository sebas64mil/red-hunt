using System.Net;

public class ClientConnection
{
    public IPEndPoint EndPoint { get; private set; }
    public int ClientId { get; private set; }
    public int LastPingMs { get; set; } = 0;
    public long LastPingTimestamp { get; set; } = 0;
    public int PingCount { get; set; } = 0;

    public ClientConnection(IPEndPoint endPoint, int clientId)
    {
        EndPoint = endPoint;
        ClientId = clientId;
    }
}