public class LeaveLobbyCommand : ILobbyCommand
{
    private readonly int playerId;

    public LeaveLobbyCommand(int playerId)
    {
        this.playerId = playerId;
    }

    public void Execute(LobbyManager lobby)
    {
        lobby.RemovePlayer(playerId);
    }
}