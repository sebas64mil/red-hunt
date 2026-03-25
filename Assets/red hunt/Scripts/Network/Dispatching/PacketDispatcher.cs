using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class PacketDispatcher
{
    private readonly ISerializer serializer;

    private Dictionary<string, Action<string, IPEndPoint>> handlers =
        new Dictionary<string, Action<string, IPEndPoint>>();

    public PacketDispatcher(ISerializer serializer)
    {
        this.serializer = serializer;
    }

    public void Register(string type, Action<string, IPEndPoint> handler)
    {
        handlers[type] = handler;
    }

    public void Dispatch(string json, IPEndPoint sender)
    {
        BasePacket basePacket = serializer.Deserialize<BasePacket>(json);

        if (basePacket == null || string.IsNullOrEmpty(basePacket.type))
        {
            Debug.LogWarning("Invalid packet");
            return;
        }

        if (handlers.TryGetValue(basePacket.type, out var handler))
        {
            handler.Invoke(json, sender);
        }
        else
        {
            Debug.LogWarning("No handler for type: " + basePacket.type);
        }
    }
}