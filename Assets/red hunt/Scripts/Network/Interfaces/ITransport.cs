using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

public interface ITransport
{
    event Action<string, IPEndPoint> OnMessageReceived;

    Task Start(int port);
    Task Send(string message, IPEndPoint endpoint);
    Task SendToAll(string message, List<IPEndPoint> clients);
    void Stop();
}