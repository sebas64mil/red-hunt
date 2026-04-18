using System;

[Serializable]
public class HealthUpdatePacket : BasePacket
{
    public int playerId;
    public int currentHealth;
    public int maxHealth;

    public HealthUpdatePacket()
    {
        type = "HEALTH_UPDATE";
    }
}