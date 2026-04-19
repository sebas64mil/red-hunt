using System;

[Serializable]
public class EscapistClueCollectedPacket : BasePacket
{
    public int escapistId;
    public string clueId;

    public EscapistClueCollectedPacket()
    {
        type = "ESCAPIST_CLUE_COLLECTED";
    }
}