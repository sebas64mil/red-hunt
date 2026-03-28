using System.Collections.Generic;
using UnityEngine;

public class SpawnManager
{
    private Dictionary<int, GameObject> players = new();

    private readonly Transform spawnParent;
    private readonly GameObject killerPrefab;
    private readonly GameObject escapistPrefab;

    private int localPlayerId;

    public SpawnManager(
        Transform spawnParent,
        GameObject killerPrefab,
        GameObject escapistPrefab,
        int localPlayerId)
    {
        this.spawnParent = spawnParent;
        this.killerPrefab = killerPrefab;
        this.escapistPrefab = escapistPrefab;
        this.localPlayerId = localPlayerId;
    }


    public void SetLocalPlayerId(int id)
    {
        localPlayerId = id;
    }

    public void AddPlayer(int id, PlayerType type)
    {
        if (players.ContainsKey(id)) return;

        GameObject prefab = type == PlayerType.Killer ? killerPrefab : escapistPrefab;
        Vector3 spawnPos = GetSpawnPosition(id, type);

        GameObject playerGO = GameObject.Instantiate(prefab, spawnPos, Quaternion.identity, spawnParent);
        playerGO.name = $"{type}_{id}";

        var view = playerGO.GetComponent<PlayerView>();
        if (view != null)
        {
            bool isLocal = id == localPlayerId;
            view.Init(id, isLocal);
        }
        else
        {
            Debug.LogWarning($"[SpawnManager] PlayerView no encontrado en {playerGO.name}");
        }

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

    public void SpawnRemotePlayer(int id, PlayerType type)
    {
        if (players.ContainsKey(id)) return;

        AddPlayer(id, type);
    }
}