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
            Debug.LogWarning("[Dispatcher] Attempted to register empty type");
            return;
        }

        if (handler == null)
        {
            Debug.LogWarning($"[Dispatcher] Null handler for type: {type}");
            return;
        }

        if (handlers.ContainsKey(type))
        {
            Debug.LogWarning($"[Dispatcher] Overwriting handler for type: {type}");
        }

        handlers[type] = handler;
    }

    public void Dispatch(string json, IPEndPoint sender)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[Dispatcher] Empty message received");
            return;
        }

        BasePacket basePacket;

        try
        {
            basePacket = serializer.Deserialize<BasePacket>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Dispatcher] Error deserializing: {e.Message}");
            return;
        }

        if (basePacket == null || string.IsNullOrEmpty(basePacket.type))
        {
            Debug.LogWarning("[Dispatcher] Invalid packet or missing type");
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
                Debug.LogError($"[Dispatcher] Error executing handler '{basePacket.type}': {e}");
            }
        }
        else
        {
            Debug.LogWarning($"[Dispatcher] No handler found for type: {basePacket.type}");
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