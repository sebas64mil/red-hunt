using UnityEngine;

public class PresentationInstaller
{
    public PresentationServices Install(LobbyUI lobbyUI, SpawnUI spawnUI, int localPlayerId)
    {
        if (lobbyUI == null)
            throw new System.Exception("LobbyUI not assigned");

        if (spawnUI == null)
            throw new System.Exception("SpawnUI not assigned");

        var killerSpawnParent = spawnUI.GetKillerSpawnParent();
        var escapistSpawnParent = spawnUI.GetEscapistSpawnParent();

        if (killerSpawnParent == null)
            throw new System.Exception("Killer SpawnParent not configured");

        if (escapistSpawnParent == null)
            throw new System.Exception("Escapist SpawnParent not configured");

        var spawnManager = new SpawnManager(
            killerSpawnParent,
            escapistSpawnParent,
            spawnUI.KillerPrefab,
            spawnUI.EscapistPrefab,
            localPlayerId,
            spawnUI.KillerSpawnPosition,
            spawnUI.EscapistBasePosition,
            spawnUI.EscapistSpacing,
            spawnUI.KillerRotationY,      
            spawnUI.EscapistRotationY    
        );

        spawnUI.Init(spawnManager);

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