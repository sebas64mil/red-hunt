public class ClientState
{
    public int PlayerId { get; private set; } = -1;

    public void SetPlayerId(int id)
    {
        PlayerId = id;
    }
}