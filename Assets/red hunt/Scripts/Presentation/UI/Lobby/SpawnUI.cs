using UnityEngine;
using System;

public class SpawnUI : MonoBehaviour
{
    [SerializeField] private GameObject killerPrefab;
    [SerializeField] private GameObject escapistPrefab;
    [SerializeField] private Transform spawnParent;

    private SpawnManager spawnManager;

    private void Awake()
    {
        spawnManager = new SpawnManager(spawnParent);
    }

    public void OnPlayerAssigned(int id, string playerType)
    {
        if (!System.Enum.TryParse<PlayerType>(playerType, true, out var type))
        {
            Debug.LogWarning($"[SpawnUI] Tipo de jugador inválido: {playerType}. Usando Escapist por defecto.");
            type = PlayerType.Escapist;
        }

        GameObject prefab = type == PlayerType.Killer ? killerPrefab : escapistPrefab;

        spawnManager.AddPlayer(id, type, prefab);
    }

    public void HandlePlayerDisconnected(int id)
    {
        spawnManager.RemovePlayer(id);
    }

    public Transform GetSpawnParent()
    {
        return spawnParent;
    }
}
