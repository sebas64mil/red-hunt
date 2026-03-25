using UnityEngine;

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
        GameObject prefab = playerType == "KILLER" ? killerPrefab : escapistPrefab;
        PlayerType type = playerType == "KILLER" ? PlayerType.Killer : PlayerType.Escapist;

        spawnManager.AddPlayer(id, type, prefab);
    }

    public void HandlePlayerDisconnected(int id)
    {
        spawnManager.RemovePlayer(id);
    }
}
