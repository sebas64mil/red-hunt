using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class UdpTransport : ITransport
{
    private UdpClient udpClient;
    private bool isRunning;
    private CancellationTokenSource cancellationTokenSource;

    public event Action<string, IPEndPoint> OnMessageReceived;

    public Task Start(int port)
    {
        udpClient = new UdpClient(port);
        isRunning = true;
        cancellationTokenSource = new CancellationTokenSource();

        Debug.Log("[Transport] UDP iniciado");

        _ = ReceiveLoop();
        return Task.CompletedTask;
    }

    private async Task ReceiveLoop()
    {
        while (isRunning && !cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync();
                string message = Encoding.UTF8.GetString(result.Buffer);

                OnMessageReceived?.Invoke(message, result.RemoteEndPoint);
            }
            catch (ObjectDisposedException)
            {
                Debug.Log("[Transport] UdpClient fue desechado, finalizando ReceiveLoop");
                break;
            }
                catch (Exception e)
            {
                if (isRunning)
                {
                    Debug.LogWarning("[Transport] Error: " + e.Message);
                }
            }
        }
    }

    public async Task Send(string message, IPEndPoint endpoint)
    {
        if (udpClient == null)
        {
            Debug.LogWarning("[Transport] Intento de enviar pero UdpClient es null");
            return;
        }

        byte[] data = Encoding.UTF8.GetBytes(message);
        await udpClient.SendAsync(data, data.Length, endpoint);
    }

    public async Task SendToAll(string message, List<IPEndPoint> clients)
    {
        if (udpClient == null)
        {
            Debug.LogWarning("[Transport] Intento de enviar a todos pero UdpClient es null");
            return;
        }

        byte[] data = Encoding.UTF8.GetBytes(message);

        foreach (var client in clients)
        {
            await udpClient.SendAsync(data, data.Length, client);
        }
    }

    public void Stop()
    {
        isRunning = false;
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        
        udpClient?.Close();
        udpClient?.Dispose();
        udpClient = null;

        Debug.Log("[Transport] UDP detenido");
    }
}