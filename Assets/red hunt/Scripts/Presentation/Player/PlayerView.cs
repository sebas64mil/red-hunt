using UnityEngine;

public class PlayerView : MonoBehaviour
{
    public int PlayerId { get; private set; }
    public bool IsHost { get; private set; }

    public void Init(int playerId, bool isHost)
    {
        PlayerId = playerId;
        IsHost = isHost;
    }

}