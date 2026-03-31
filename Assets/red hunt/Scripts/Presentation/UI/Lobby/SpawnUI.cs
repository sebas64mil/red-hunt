using UnityEngine;
using System;

public class SpawnUI : MonoBehaviour
{
    [SerializeField] private GameObject killerPrefab;
    [SerializeField] private GameObject escapistPrefab;
    [SerializeField] private Transform spawnParent;

    [Header("Spawn Positions")]
    [SerializeField] private Vector3 hostSpawnPosition = Vector3.zero;
    [SerializeField] private Vector3 clientBasePosition = new Vector3(0f, 0f, 5f);
    [SerializeField] private float clientSpacing = 2f;


    private SpawnManager spawnManager;

    public GameObject KillerPrefab => killerPrefab;
    public GameObject EscapistPrefab => escapistPrefab;


    public Vector3 HostSpawnPosition => hostSpawnPosition;
    public Vector3 ClientBasePosition => clientBasePosition;
    public float ClientSpacing => clientSpacing;

    public void Init(SpawnManager spawnManager)
    {
        this.spawnManager = spawnManager;
    }

    private void OnEnable()
    {
        try
        {
            LobbyBootstrap.Instance?.RegisterSpawnUI(this);
        }
        catch (Exception)
        {
            Debug.LogWarning("[SpawnUI] Registro con GameBootstrap falló");
        }
    }

    private void OnDisable()
    {
        try
        {
            LobbyBootstrap.Instance?.UnregisterSpawnUI(this);
        }
        catch (Exception) { }
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