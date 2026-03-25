using System;

[Serializable]
public class PlayerPacket : BasePacket
{
    public int id;
    public string playerType; 
}