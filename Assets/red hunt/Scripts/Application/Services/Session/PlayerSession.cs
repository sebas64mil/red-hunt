public class PlayerSession
{
    public int Id { get; private set; }
    public string PlayerType { get; private set; }
    public bool IsConnected { get; private set; }

    public bool IsReady { get; private set; }


    public PlayerSession(int id, string playerType)
    {
        Id = id;
        PlayerType = playerType;
        IsConnected = true;
    }

    public void Disconnect()
    {
        if (!IsConnected) return;

        IsConnected = false;
    }

    public void Reconnect()
    {
        if (IsConnected) return;

        IsConnected = true;
    }

    public void SetReady(bool ready)
    {
        IsReady = ready;
    }

}