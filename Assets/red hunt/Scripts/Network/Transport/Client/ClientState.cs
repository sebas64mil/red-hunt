using System;

public class ClientState
{
    public int PlayerId { get; private set; } = -1;
    public string PendingPlayerType { get; private set; }
    public bool IsConnected { get; private set; } = false;


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

    public void SetConnected(bool connected)
    {
        IsConnected = connected;
    }
}