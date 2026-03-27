using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

public class Client : MonoBehaviour, IClient
{
    private ITransport transport;
    private IPEndPoint serverEndPoint;

    private PacketDispatcher dispatcher;

    public bool isServerConnected = false;

    public event Action<string, IPEndPoint> OnMessageReceived;
    public event Action OnDisconnected;

    [HideInInspector] public bool isConnected { get; private set; }

    public void Init(ITransport transport, PacketDispatcher dispatcher)
    {
        this.transport = transport;
        this.dispatcher = dispatcher;

        transport.OnMessageReceived += HandleMessage;
    }

    private void HandleMessage(string msg, IPEndPoint sender)
    {
        Debug.Log("[Client] Recibido: " + msg);

        OnMessageReceived?.Invoke(msg, sender);

        dispatcher.Dispatch(msg, sender); 
    }

    public async Task ConnectToServer(string ipAddress, int port)
    {
        serverEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

        await transport.Start(0);
        isConnected = true;

        await SendMessageAsync("{\"type\":\"CONNECT\"}"); 
    }

    public async Task SendMessageAsync(string message)
    {
        if (!isConnected)
        {
            Debug.Log("[Client] Not connected");
            return;
        }

        await transport.Send(message, serverEndPoint);

        Debug.Log("[Client] Sent: " + message);
    }

    public void Disconnect()
    {
        if (!isConnected) return;

        isConnected = false;

        transport.Stop();

        Debug.Log("[Client] Disconnected");
        OnDisconnected?.Invoke();
    }

    private async void OnDestroy()
    {
        Disconnect();
        await Task.Delay(100);
    }
}