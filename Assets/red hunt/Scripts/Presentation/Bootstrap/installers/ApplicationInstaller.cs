using UnityEngine;

public class ApplicationInstaller
{
    public ApplicationServices Install()
    {
        Debug.Log("[ApplicationInstaller] Iniciando instalación de Application...");

        // ==================== PLAYER REGISTRY ====================
        var playerRegistry = new PlayerRegistry();
        Debug.Log("[ApplicationInstaller]  PlayerRegistry creado");

        // ==================== LOBBY MANAGER ====================
        var lobbyManager = new LobbyManager(playerRegistry);
        Debug.Log("[ApplicationInstaller]  LobbyManager creado");

        Debug.Log("[ApplicationInstaller]  Application inicializado completamente");

        return new ApplicationServices(
            playerRegistry,
            lobbyManager
        );
    }
}
public class ApplicationServices
{
    public PlayerRegistry PlayerRegistry { get; }
    public LobbyManager LobbyManager { get; }

    public ApplicationServices(
        PlayerRegistry playerRegistry,
        LobbyManager lobbyManager)
    {
        PlayerRegistry = playerRegistry;
        LobbyManager = lobbyManager;
    }
}
