using UnityEngine;
using System;

public class SpawnUI : MonoBehaviour
{
    [SerializeField] private GameObject killerPrefab;
    [SerializeField] private GameObject escapistPrefab;

    [SerializeField] private Transform killerSpawnParent;
    [SerializeField] private Transform escapistSpawnParent;

    [Header("Spawn Positions")]
    [SerializeField] private Vector3 killerSpawnPosition = Vector3.zero;
    [SerializeField] private Vector3 escapistBasePosition = new Vector3(0f, 0f, 5f);
    [SerializeField] private float escapistSpacing = 2f;

    [Header("Spawn Rotations (Y axis in degrees)")]
    [SerializeField] private float killerRotationY = 0f;
    [SerializeField] private float escapistRotationY = 0f;

    private SpawnManager spawnManager;

    public GameObject KillerPrefab => killerPrefab;
    public GameObject EscapistPrefab => escapistPrefab;

    public Transform KillerSpawnParent => killerSpawnParent;
    public Transform EscapistSpawnParent => escapistSpawnParent;

    public Vector3 KillerSpawnPosition => killerSpawnPosition;
    public Vector3 EscapistBasePosition => escapistBasePosition;
    public float EscapistSpacing => escapistSpacing;

    public float KillerRotationY => killerRotationY;
    public float EscapistRotationY => escapistRotationY;

    public void Init(SpawnManager spawnManager)
    {
        this.spawnManager = spawnManager;
       
    }

    private void OnEnable()
    {
        try
        {
            ModularLobbyBootstrap.Instance?.RegisterSpawnUI(this);
        }
        catch (Exception)
        {
            Debug.LogWarning("[SpawnUI] Registration with GameBootstrap failed");
        }
    }

    private void OnDisable()
    {
        try
        {
            ModularLobbyBootstrap.Instance?.UnregisterSpawnUI(this);
        }
        catch (Exception) { }
    }

    public void OnPlayerAssigned(int id, string playerType)
    {
        if (spawnManager == null)
        {
            Debug.LogError("[SpawnUI] SpawnManager not initialized");
            return;
        }

        if (!Enum.TryParse<PlayerType>(playerType, true, out var type))
        {
            Debug.LogWarning($"[SpawnUI] Invalid type: {playerType}, using Escapist");
            type = PlayerType.Escapist;
        }

        spawnManager.AddPlayer(id, type);
    }

    public void HandlePlayerDisconnected(int id)
    {
        if (spawnManager == null)
        {
            Debug.LogError("[SpawnUI] SpawnManager not initialized");
            return;
        }

        spawnManager.RemovePlayer(id);
    }

    public Transform GetKillerSpawnParent() => killerSpawnParent;
    public Transform GetEscapistSpawnParent() => escapistSpawnParent;
}