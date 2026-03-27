public class PacketBuilder
{
    private readonly ISerializer serializer;

    public PacketBuilder(ISerializer serializer)
    {
        this.serializer = serializer;
    }

    public string CreateAssignPlayer(int id)
    {
        AssignPlayerPacket packet = new AssignPlayerPacket
        {
            type = "ASSIGN_PLAYER",
            id = id
        };

        return serializer.Serialize(packet);
    }

    public string CreatePlayer(int id, string playerType)
    {
        PlayerPacket packet = new PlayerPacket
        {
            type = "PLAYER",
            id = id,
            playerType = playerType
        };

        return serializer.Serialize(packet);
    }

    public string CreatePlayerReady(int id)
    {
        var packet = new PlayerReadyPacket
        {
            type = "PLAYER_READY",
            id = id
        };

        return serializer.Serialize(packet);
    }
}