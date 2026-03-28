using System.Collections.Generic;

[System.Serializable]
public class PlayerInfoLobby
{
    public int Id;
    public string Name;
    public bool IsHost;
}

[System.Serializable]
public class LobbyStatePacket
{
    public string type;
    public List<PlayerInfoLobby> Players;
}