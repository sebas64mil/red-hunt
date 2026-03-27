using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

public class Server : MonoBehaviour, IServer
{
    private ITransport transport;
    private PacketDispatcher dispatcher;
    private ISerializer serializer;

    public bool isServerRunning { get; private set; }

    public event Action<string, IPEndPoint> OnMessageReceived;
    public event Action<IPEndPoint> OnClientMessage;
    public event Action OnDisconnected;

    public void Init(ITransport transport, PacketDispatcher dispatcher, ISerializer serializer)
    {
        this.transport = transport;
        this.dispatcher = dispatcher;
        this.serializer = serializer;

        transport.OnMessageReceived += HandleMessage;
    }

    public void HandleMessage(string msg, IPEndPoint sender)
    {
        Debug.Log($"[Server] Recibido: {msg}");

        OnMessageReceived?.Invoke(msg, sender);
        OnClientMessage?.Invoke(sender); 

        dispatcher.Dispatch(msg, sender);
    }


    public async Task StartServer(int port)
    {
        await transport.Start(port);
        isServerRunning = true;

        Debug.Log("[Server] Server iniciado");
    }

    public async Task SendToClientAsync(string message, IPEndPoint client)
    {
        if (!isServerRunning) return;

        await transport.Send(message, client);
    }

    public void Disconnect()
    {
        if (!isServerRunning) return;

        isServerRunning = false;

        transport.Stop();

        Debug.Log("[Server] Desconectado");

        OnDisconnected?.Invoke();
    }

    private async void OnDestroy()
    {
        Disconnect();
        await Task.Delay(100);
    }
}