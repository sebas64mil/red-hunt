public class PacketBuilder
{
    public PlayerPacket CreatePlayer(int id, string playerType)
    {
        return new PlayerPacket
        {
            type = "PLAYER",
            id = id,
            playerType = playerType
        };
    }
}