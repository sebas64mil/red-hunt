using System;
using UnityEngine;

[Serializable]
public class MovePacket : BasePacket
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
    public long timestamp;

    public MovePacket()
    {
        type = "MOVE";
    }

    public Vector3 GetPosition() => new Vector3(posX, posY, posZ);

    public Quaternion GetRotation() => new Quaternion(rotX, rotY, rotZ, rotW);

    public Vector3 GetVelocity() => new Vector3(velocityX, velocityY, velocityZ);

    public void SetFromTransform(int id, Transform transform, Vector3 velocity, bool jumping, long timestamp)
    {
        playerId = id;
        posX = transform.position.x;
        posY = transform.position.y;
        posZ = transform.position.z;
        rotX = transform.rotation.x;
        rotY = transform.rotation.y;
        rotZ = transform.rotation.z;
        rotW = transform.rotation.w;
        velocityX = velocity.x;
        velocityY = velocity.y;
        velocityZ = velocity.z;
        isJumping = jumping;
        this.timestamp = timestamp;
    }
}

