using System;

public class ClientState
{
    public int PlayerId { get; private set; } = -1;
    public string PendingPlayerType { get; private set; }

    public event Action<int> OnPlayerIdAssigned;

    public void SetPlayerId(int id)
    {
        PlayerId = id;
        OnPlayerIdAssigned?.Invoke(id);
    }

    public void SetPendingPlayerType(string type)
    {
        PendingPlayerType = type;
    }

    public void ClearPendingPlayerType()
    {
        PendingPlayerType = null;
    }
}