public class JoinLobbyCommand : ILobbyCommand
{
    private readonly string playerType;

    public JoinLobbyCommand(string playerType)
    {
        this.playerType = playerType;
    }

    public void Execute(LobbyManager lobby)
    {
        lobby.AddPlayer(playerType);
    }
}