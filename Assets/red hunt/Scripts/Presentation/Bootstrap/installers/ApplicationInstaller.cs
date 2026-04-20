using UnityEngine;

public class ApplicationInstaller
{
    public ApplicationServices Install()
    {
        var playerRegistry = new PlayerRegistry();

        var lobbyManager = new LobbyManager(playerRegistry);

        var clueRegistry = new EscapistClueRegistry();

        return new ApplicationServices(
            playerRegistry,
            lobbyManager,
            clueRegistry
        );
    }
}

public class ApplicationServices
{
    public PlayerRegistry PlayerRegistry { get; }
    public LobbyManager LobbyManager { get; }
    public EscapistClueRegistry ClueRegistry { get; }

    public ApplicationServices(
        PlayerRegistry playerRegistry,
        LobbyManager lobbyManager,
        EscapistClueRegistry clueRegistry)
    {
        PlayerRegistry = playerRegistry;
        LobbyManager = lobbyManager;
        ClueRegistry = clueRegistry;
    }
}
