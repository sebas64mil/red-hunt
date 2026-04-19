using System;

[Serializable]
public class EscapistPassedPacket : BasePacket
{
    public int escapistId;

    public EscapistPassedPacket()
    {
        type = "ESCAPIST_PASSED";
    }
}