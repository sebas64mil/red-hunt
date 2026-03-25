using System.Collections.Generic;
using UnityEngine;

public class SpawnManager
{
    private Dictionary<int, Player> players = new Dictionary<int, Player>();
    private readonly Transform spawnParent;

    public SpawnManager(Transform spawnParent)
    {
        this.spawnParent = spawnParent;
    }

    public void AddPlayer(int id, PlayerType type, GameObject prefab)
    {
        if (players.ContainsKey(id)) return;

        players[id] = new Player(id, type);

        Vector3 spawnPos = GetSpawnPosition(id, type);
        GameObject playerGO = GameObject.Instantiate(prefab, spawnPos, Quaternion.identity, spawnParent);
        playerGO.name = $"{type}_{id}";
    }

    private Vector3 GetSpawnPosition(int id, PlayerType type)
    {
        if (type == PlayerType.Killer)
            return new Vector3(0, 0, 0);
        else
            return new Vector3(id * 2f, 0, 5f);
    }

    public void RemovePlayer(int id)
    {
        if (!players.ContainsKey(id)) return;

        GameObject go = GameObject.Find($"{players[id].Type}_{id}");
        if (go != null) GameObject.Destroy(go);

        players.Remove(id);
    }
}