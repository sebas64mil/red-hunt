using System;
using System.Collections.Generic;
using UnityEngine;

public class AdminUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject playerEntryPrefab;

    private readonly Dictionary<int, AdminPlayerEntry> entries = new();

    public event Action<int> OnKickRequested;

    private void Awake()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public void SetIsHost(bool isHost)
    {
        if (rootPanel != null)
            rootPanel.SetActive(isHost);
    }

    public void AddPlayerEntry(int playerId)
    {
        if (entries.ContainsKey(playerId)) return;
        if (playerEntryPrefab == null || contentParent == null) return;

        var go = Instantiate(playerEntryPrefab, contentParent, false);
        var entry = go.GetComponent<AdminPlayerEntry>();

        if (entry == null)
        {
            Debug.LogWarning("[AdminUI] Prefab no contiene AdminPlayerEntry");
            Destroy(go);
            return;
        }

        entry.Setup(playerId, HandleKickClicked);

        entries[playerId] = entry;
    }

    private void HandleKickClicked(int playerId)
    {
        Debug.Log($"[AdminUI] Kick solicitado para player {playerId}");

        OnKickRequested?.Invoke(playerId);
    }

    public void RemovePlayerEntry(int playerId)
    {
        if (!entries.TryGetValue(playerId, out var entry)) return;

        Destroy(entry.gameObject);
        entries.Remove(playerId);
    }

    public void ClearAll()
    {
        foreach (var kv in entries)
        {
            if (kv.Value != null)
                Destroy(kv.Value.gameObject);
        }

        entries.Clear();
    }
}