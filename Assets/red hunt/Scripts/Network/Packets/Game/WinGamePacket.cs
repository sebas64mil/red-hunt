using System;

[Serializable]
public class WinGamePacket : BasePacket
{
    public int winnerId;             
    public string winnerType;        
    public bool isKillerWin;         
}