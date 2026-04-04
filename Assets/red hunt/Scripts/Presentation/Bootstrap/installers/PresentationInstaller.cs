using UnityEngine;

public class PresentationInstaller
{
    public PresentationServices Install(LobbyUI lobbyUI, SpawnUI spawnUI, int localPlayerId)
    {
        Debug.Log("[PresentationInstaller] Iniciando instalación...");

        if (lobbyUI == null)
            throw new System.Exception("LobbyUI no asignado");

        if (spawnUI == null)
            throw new System.Exception("SpawnUI no asignado");

        var killerSpawnParent = spawnUI.GetKillerSpawnParent();
        var escapistSpawnParent = spawnUI.GetEscapistSpawnParent();

        if (killerSpawnParent == null)
            throw new System.Exception("Killer SpawnParent no configurado");

        if (escapistSpawnParent == null)
            throw new System.Exception("Escapist SpawnParent no configurado");

        // ✅ Pasar también las rotaciones Y
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