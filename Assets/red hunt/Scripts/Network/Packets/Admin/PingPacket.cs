using System;

[Serializable]
public class PingPacket : BasePacket
{
    public long pingTimestamp;
    public int clientId;
}
