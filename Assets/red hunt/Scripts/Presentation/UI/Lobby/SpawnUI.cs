using UnityEngine;
using System;

public class SpawnUI : MonoBehaviour
{
    [SerializeField] private GameObject killerPrefab;
    [SerializeField] private GameObject escapistPrefab;
    [SerializeField] private Transform spawnParent;

    private SpawnManager spawnManager;

    // Exponer los prefabs
    public GameObject KillerPrefab => killerPrefab;
    public GameObject EscapistPrefab => escapistPrefab;

    private void Awake()
    {
        spawnManager = new SpawnManager(spawnParent, killerPrefab, escapistPrefab);
    }

    public void OnPlayerAssigned(int id, string playerType)
    {
        if (!Enum.TryParse<PlayerType>(playerType, true, out var type))
        {
            Debug.LogWarning($"[SpawnUI] Tipo de jugador inválido: {playerType}. Usando Escapist por defecto.");
            type = PlayerType.Escapist;
        }

        spawnManager.AddPlayer(id, type);
    }

    public void HandlePlayerDisconnected(int id)
    {
        spawnManager.RemovePlayer(id);
    }

    public Transform GetSpawnParent()
    {
        return spawnParent;
    }

    public SpawnManager GetSpawnManager() => spawnManager;
}