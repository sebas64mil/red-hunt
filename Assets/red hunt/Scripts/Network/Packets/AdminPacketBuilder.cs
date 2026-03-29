using System;
public class AdminPacketBuilder
{
    private readonly ISerializer serializer;

    public AdminPacketBuilder(ISerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public string CreateKick(int targetId)
    {
        var packet = new KickPacket
        {
            type = "ADMIN_KICK",
            targetId = targetId
        };

        return serializer.Serialize(packet);
    }
}