using UnityEngine;
using System;

public class SpawnUI : MonoBehaviour
{
    [SerializeField] private GameObject killerPrefab;
    [SerializeField] private GameObject escapistPrefab;
    [SerializeField] private Transform spawnParent;

    private SpawnManager spawnManager;

    public GameObject KillerPrefab => killerPrefab;
    public GameObject EscapistPrefab => escapistPrefab;

    public void Init(SpawnManager spawnManager)
    {
        this.spawnManager = spawnManager;
    }

    public void OnPlayerAssigned(int id, string playerType)
    {
        if (spawnManager == null)
        {
            Debug.LogError("[SpawnUI] SpawnManager no inicializado");
            return;
        }

        if (!Enum.TryParse<PlayerType>(playerType, true, out var type))
        {
            Debug.LogWarning($"[SpawnUI] Tipo inválido: {playerType}, usando Escapist");
            type = PlayerType.Escapist;
        }

        spawnManager.AddPlayer(id, type);
    }

    public void HandlePlayerDisconnected(int id)
    {
        if (spawnManager == null)
        {
            Debug.LogError("[SpawnUI] SpawnManager no inicializado");
            return;
        }

        spawnManager.RemovePlayer(id);
    }

    public Transform GetSpawnParent()
    {
        return spawnParent;
    }
}