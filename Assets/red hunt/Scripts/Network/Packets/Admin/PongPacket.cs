using System;

[Serializable]
public class PongPacket : BasePacket
{
    public long pingTimestamp;
    public int clientId;
}
