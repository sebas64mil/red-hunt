using System.Collections.Generic;
using UnityEngine;

public class SpawnManager
{
    private Dictionary<int, GameObject> players = new();
    private readonly Transform spawnParent;

    public SpawnManager(Transform spawnParent)
    {
        this.spawnParent = spawnParent;
    }

    public void AddPlayer(int id, PlayerType type, GameObject prefab)
    {
        if (players.ContainsKey(id)) return;

        Vector3 spawnPos = GetSpawnPosition(id, type);

        GameObject playerGO = GameObject.Instantiate(prefab, spawnPos, Quaternion.identity, spawnParent);
        playerGO.name = $"{type}_{id}";

        players[id] = playerGO;
    }

    public void RemovePlayer(int id)
    {
        if (!players.ContainsKey(id)) return;

        GameObject.Destroy(players[id]);
        players.Remove(id);
    }

    private Vector3 GetSpawnPosition(int id, PlayerType type)
    {
        if (type == PlayerType.Killer)
            return new Vector3(0, 0, 0);

        return new Vector3(id * 2f, 0, 5f);
    }
}