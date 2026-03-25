public class Player
{
    public int Id { get; private set; }
    public PlayerType Type { get; private set; }

    public Player(int id, PlayerType type)
    {
        Id = id;
        Type = type;
    }
}