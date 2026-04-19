using System;
using System.Collections.Generic;

[Serializable]
public class EscapistsPassedSnapshotPacket : BasePacket
{
    public List<int> targetEscapistIds = new();
    public List<int> passedEscapistIds = new();

    public EscapistsPassedSnapshotPacket()
    {
        type = "ESCAPISTS_PASSED_SNAPSHOT";
    }
}