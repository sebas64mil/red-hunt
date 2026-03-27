using UnityEngine;

public class PresentationInstaller
{
    public PresentationServices Install(LobbyUI lobbyUI, SpawnUI spawnUI)
    {
        Debug.Log("[PresentationInstaller] Iniciando instalación...");

        if (lobbyUI == null)
            throw new System.Exception("LobbyUI no asignado");

        if (spawnUI == null)
            throw new System.Exception("SpawnUI no asignado");

        var spawnParent = spawnUI.GetSpawnParent();

        if (spawnParent == null)
            throw new System.Exception("SpawnParent no configurado");

        var spawnManager = new SpawnManager(spawnParent);

        return new PresentationServices
        {
            LobbyUI = lobbyUI,
            SpawnUI = spawnUI,
            SpawnManager = spawnManager
        };
    }
}

public class PresentationServices
{
    public LobbyUI LobbyUI { get; set; }
    public SpawnUI SpawnUI { get; set; }
    public SpawnManager SpawnManager { get; set; }
}
