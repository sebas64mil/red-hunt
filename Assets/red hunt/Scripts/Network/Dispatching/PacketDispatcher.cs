using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class PacketDispatcher
{
    private readonly ISerializer serializer;

    private readonly Dictionary<string, Action<string, IPEndPoint>> handlers =
        new Dictionary<string, Action<string, IPEndPoint>>();

    public PacketDispatcher(ISerializer serializer)
    {
        this.serializer = serializer;
    }


    public void Register(string type, Action<string, IPEndPoint> handler)
    {
        if (string.IsNullOrEmpty(type))
        {
            Debug.LogWarning("[Dispatcher] Intento de registrar tipo vacío");
            return;
        }

        if (handler == null)
        {
            Debug.LogWarning($"[Dispatcher] Handler nulo para tipo: {type}");
            return;
        }

        if (handlers.ContainsKey(type))
        {
            Debug.LogWarning($"[Dispatcher] Sobrescribiendo handler para tipo: {type}");
        }

        handlers[type] = handler;
    }

    public void Dispatch(string json, IPEndPoint sender)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[Dispatcher] Mensaje vacío recibido");
            return;
        }

        BasePacket basePacket;

        try
        {
            basePacket = serializer.Deserialize<BasePacket>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Dispatcher] Error al deserializar: {e.Message}");
            return;
        }

        if (basePacket == null || string.IsNullOrEmpty(basePacket.type))
        {
            Debug.LogWarning("[Dispatcher] Packet inválido o sin tipo");
            return;
        }

        if (handlers.TryGetValue(basePacket.type, out var handler))
        {
            try
            {
                handler.Invoke(json, sender);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Dispatcher] Error ejecutando handler '{basePacket.type}': {e}");
            }
        }
        else
        {
            Debug.LogWarning($"[Dispatcher] No hay handler para tipo: {basePacket.type}");
        }
    }

    public void Unregister(string type)
    {
        if (handlers.ContainsKey(type))
        {
            handlers.Remove(type);
        }
    }


    public void Clear()
    {
        handlers.Clear();
    }
}