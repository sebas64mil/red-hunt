using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerStateSnapshot : BasePacket
{
    public List<PlayerStateData> players = new List<PlayerStateData>();
    public long timestamp;

    public PlayerStateSnapshot()
    {
        type = "PLAYER_STATE_SNAPSHOT";
    }
}

[Serializable]
public class PlayerStateData
{
    public int playerId;
    public float posX;
    public float posY;
    public float posZ;
    public float rotX;
    public float rotY;
    public float rotZ;
    public float rotW;
    public float velocityX;
    public float velocityY;
    public float velocityZ;
    public bool isJumping;

    public Vector3 GetPosition() => new Vector3(posX, posY, posZ);
    public Quaternion GetRotation() => new Quaternion(rotX, rotY, rotZ, rotW);
    public Vector3 GetVelocity() => new Vector3(velocityX, velocityY, velocityZ);
}