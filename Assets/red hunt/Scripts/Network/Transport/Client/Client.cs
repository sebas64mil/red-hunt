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

        try
        {
            await transport.Start(0);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Client] Error al iniciar transporte: " + ex.Message);
            try { transport.Stop(); } catch { }
            isConnected = false;
            throw;
        }

        isConnected = true;

        try
        {
            await SendMessageAsync("{\"type\":\"CONNECT\"}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[Client] Error enviando CONNECT: " + ex.Message);
            Disconnect();
            throw;
        }
    }

    public async Task SendMessageAsync(string message)
    {
        if (!isConnected)
        {
            Debug.Log("[Client] Not connected");
            return;
        }

        try
        {
            await transport.Send(message, serverEndPoint);
            Debug.Log("[Client] Sent: " + message);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Client] Error en SendMessageAsync: " + e.Message);
            throw;
        }
    }

    public void Disconnect()
    {

        if (!isConnected)
        {
            try
            {
                transport?.Stop();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Client] Error forzando Stop en Disconnect: " + ex.Message);
            }
            isConnected = false;
            Debug.Log("[Client] Disconnected (forced)");
            OnDisconnected?.Invoke();
            return;
        }

        isConnected = false;

        try
        {
            transport.Stop();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Client] Error al detener transporte en Disconnect: " + ex.Message);
        }

        Debug.Log("[Client] Disconnected");
        OnDisconnected?.Invoke();
    }

    private async void OnDestroy()
    {
        Disconnect();
        await Task.Delay(100);
    }
}