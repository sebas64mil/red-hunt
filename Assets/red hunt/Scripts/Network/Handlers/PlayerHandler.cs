using System.Net;
using UnityEngine;

public class PlayerHandler
{
    private readonly ISerializer serializer;

    public PlayerHandler(ISerializer serializer)
    {
        this.serializer = serializer;
    }

    public void Handle(string json, IPEndPoint sender)
    {
        PlayerPacket packet = serializer.Deserialize<PlayerPacket>(json);

        Debug.Log($"Player ID: {packet.id}");
        Debug.Log($"Player Type: {packet.playerType}");
        Debug.Log($"From: {sender}");
    }
}