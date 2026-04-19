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
    private LatencyService latencyService;
    private bool latencyServiceSearched = false;

    public event Action<int> OnKickRequested;

    private void Awake()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);

    }

    private void Update()
    {
        // Buscar LatencyService si no se ha encontrado aún
        if (latencyService == null && !latencyServiceSearched)
        {
            latencyService = FindFirstObjectByType<LatencyService>();
            latencyServiceSearched = true;
            if (latencyService != null)
            {
                Debug.Log("[AdminUI] LatencyService encontrado en Update");
            }
        }

        if (latencyService == null || entries.Count == 0) return;

        foreach (var kvp in entries)
        {
            int playerId = kvp.Key;
            var entry = kvp.Value;

            int latency = latencyService.GetClientLatency(playerId);
            entry.UpdateLatency(latency);
        }
    }

    private void OnEnable()
    {
        try
        {
            ModularLobbyBootstrap.Instance?.RegisterAdminUI(this);

            DetectAndApplyHostStatus();
            RepopulatePlayersFromLobby();

            // Buscar LatencyService
            if (latencyService == null)
            {
                latencyService = FindFirstObjectByType<LatencyService>();
                if (latencyService != null)
                {
                    Debug.Log("[AdminUI] LatencyService encontrado en escena");
                }
                else
                {
                    Debug.Log("[AdminUI] LatencyService no encontrado, se buscará en Update");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AdminUI]  Error en OnEnable: {ex.Message}");
        }
    }

    private void OnDisable()
    {
        try
        {
            ModularLobbyBootstrap.Instance?.UnregisterAdminUI(this);
        }
        catch (Exception) { }
    }

    private void RepopulatePlayersFromLobby()
    {
        try
        {
            var lobbyManager = ModularLobbyBootstrap.Instance?.GetLobbyManager();
            if (lobbyManager == null)
            {
                return;
            }

            var allPlayers = lobbyManager.GetAllPlayers();
            if (allPlayers == null)
            {
                Debug.LogWarning("[AdminUI] No hay players en LobbyManager");
                return;
            }

            ClearAll();

            foreach (var player in allPlayers)
            {
                AddPlayerEntry(player.Id);
            }

        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AdminUI] Error repoblando: {ex.Message}");
        }
    }

    private void DetectAndApplyHostStatus()
    {
        try
        {
            var lobbyNetworkService = ModularLobbyBootstrap.Instance?.GetLobbyNetworkService();
            if (lobbyNetworkService == null)
            {
                return;
            }

            bool isHost = lobbyNetworkService.IsHost;
            SetIsHost(isHost);
            
            Debug.Log($"[AdminUI] Host status detectado: {isHost}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AdminUI] Error detectando host status: {ex.Message}");
        }
    }

    public void SetIsHost(bool isHost)
    {
        if (rootPanel != null)
            rootPanel.SetActive(isHost);

    }

    public void AddPlayerEntry(int playerId)
    {
        if (entries.ContainsKey(playerId))
        {
            Debug.LogWarning($"[AdminUI] Entrada para player {playerId} ya existe");
            return;
        }

        if (playerEntryPrefab == null)
        {
            Debug.LogError("[AdminUI] playerEntryPrefab es NULL");
            return;
        }

        if (contentParent == null)
        {
            Debug.LogError("[AdminUI] contentParent es NULL");
            return;
        }

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
        OnKickRequested?.Invoke(playerId);
    }

    public void RemovePlayerEntry(int playerId)
    {
        if (!entries.TryGetValue(playerId, out var entry))
        {
            Debug.LogWarning($"[AdminUI] Entrada para player {playerId} no existe");
            return;
        }

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

    public void SetLatencyService(LatencyService latencyService)
    {
        this.latencyService = latencyService;
    }
}