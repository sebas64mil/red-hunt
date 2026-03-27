using System.Net;

public class ClientConnection
{
    public IPEndPoint EndPoint { get; private set; }
    public int ClientId { get; private set; }

    public ClientConnection(IPEndPoint endPoint, int clientId)
    {
        EndPoint = endPoint;
        ClientId = clientId;
    }
}