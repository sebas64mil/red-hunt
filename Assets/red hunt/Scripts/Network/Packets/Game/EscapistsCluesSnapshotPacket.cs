using System;
using System.Collections.Generic;

[Serializable]
public class EscapistsCluesSnapshotPacket : BasePacket
{
    public List<int> targetEscapistIds = new();
    public List<EscapistCluesEntry> entries = new();

    public EscapistsCluesSnapshotPacket()
    {
        type = "ESCAPISTS_CLUES_SNAPSHOT";
    }
}

[Serializable]
public class EscapistCluesEntry
{
    public int escapistId;
    public List<string> clueIds = new();
}