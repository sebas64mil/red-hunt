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

        if (!isServerConnected && msg != null && msg.Contains("CONNECT_ACK"))
        {
            Debug.Log("[Client] Conexión confirmada por el server");
            isServerConnected = true;

            isConnected = true;
        }

        OnMessageReceived?.Invoke(msg, sender);

        dispatcher.Dispatch(msg, sender);
    }

    public async Task<bool> ConnectToServer(string ipAddress, int port)
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
            return false;
        }

        isConnected = false;
        isServerConnected = false;

        try
        {
            await SendMessageAsync("{\"type\":\"CONNECT\"}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[Client] Error enviando CONNECT: " + ex.Message);
            Disconnect();
            return false;
        }

        int timeoutMs = 3000;
        int elapsed = 0;
        int step = 100;

        while (!isServerConnected && elapsed < timeoutMs)
        {
            await Task.Delay(step);
            elapsed += step;
        }

        if (!isServerConnected)
        {
            Debug.LogWarning("[Client] Timeout: server no responde al CONNECT");

            try
            {
                await SendMessageAsync("{\"type\":\"DISCONNECT\"}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Client] Error enviando DISCONNECT tras timeout: " + ex.Message);
            }

            Disconnect();
            return false;
        }

        isConnected = true;
        Debug.Log("[Client] Conectado correctamente al server");
        return true;
    }
    public async Task SendMessageAsync(string message)
    {
        if (transport == null || serverEndPoint == null)
        {
            Debug.Log("[Client] Transport o serverEndPoint no inicializado");
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