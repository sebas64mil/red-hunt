using System;

[Serializable]
public class AssignRejectPacket : BasePacket
{
    public int id;
    public string reason;
}