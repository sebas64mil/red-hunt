public class ClientState
{
    public int PlayerId { get; private set; } = -1;
    public string PendingPlayerType { get; private set; }

    public void SetPlayerId(int id)
    {
        PlayerId = id;
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