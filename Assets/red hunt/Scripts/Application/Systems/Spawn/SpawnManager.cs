using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class SpawnManager
{
    private Dictionary<int, GameObject> players = new();
    private Dictionary<int, PlayerType> playerTypes = new();

    private readonly Transform killerSpawnParent;
    private readonly Transform escapistSpawnParent;

    private readonly GameObject killerPrefab;
    private readonly GameObject escapistPrefab;

    private readonly Vector3 killerSpawnPosition;
    private readonly Vector3 escapistBasePosition;
    private readonly float escapistSpacing;

    private readonly float killerRotationY;
    private readonly float escapistRotationY;

    private int localPlayerId;

    public SpawnManager(
        Transform killerSpawnParent,
        Transform escapistSpawnParent,
        GameObject killerPrefab,
        GameObject escapistPrefab,
        int localPlayerId,
        Vector3 killerSpawnPosition,
        Vector3 escapistBasePosition,
        float escapistSpacing,
        float killerRotationY = 0f,
        float escapistRotationY = 0f)
    {
        this.killerSpawnParent = killerSpawnParent;
        this.escapistSpawnParent = escapistSpawnParent;
        this.killerPrefab = killerPrefab;
        this.escapistPrefab = escapistPrefab;
        this.localPlayerId = localPlayerId;

        this.killerSpawnPosition = killerSpawnPosition;
        this.escapistBasePosition = escapistBasePosition;
        this.escapistSpacing = escapistSpacing;
        this.killerRotationY = killerRotationY;
        this.escapistRotationY = escapistRotationY;

    }

    public SpawnManager(
        Transform spawnParent,
        GameObject killerPrefab,
        GameObject escapistPrefab,
        int localPlayerId,
        Vector3 hostSpawnPosition,
        Vector3 clientBasePosition,
        float clientSpacing)
        : this(
            spawnParent,
            spawnParent,
            killerPrefab,
            escapistPrefab,
            localPlayerId,
            hostSpawnPosition,
            clientBasePosition,
            clientSpacing,
            0f,
            0f)
    {
        Debug.Log("[SpawnManager] ⚠️ Usando constructor antiguo - ambos tipos usan el mismo spawnParent");
    }

    public void SetLocalPlayerId(int id)
    {
        localPlayerId = id;
    }

    public bool HasPlayer(int id) => players.ContainsKey(id);

    public void AddPlayer(int id, PlayerType type)
    {
        if (players.ContainsKey(id))
        {
            Debug.LogWarning($"[SpawnManager] Player con id {id} ya existe. Ignorando AddPlayer.");
            return;
        }

        Debug.Log($"[SpawnManager] 👤 Agregando player {id} de tipo {type}");

        // ✅ NUEVO: Calcular índice basado en cuántos Escapists ya existen
        int escapistIndex = 0;
        if (type == PlayerType.Escapist)
        {
            // Contar cuántos Escapists ya están spawnead os
            escapistIndex = playerTypes.Values.Count(t => t == PlayerType.Escapist);
            
            Debug.Log($"[SpawnManager] 📍 Escapista {id} con índice {escapistIndex} (basado en {escapistIndex} Escapistas ya spawnead os)");
        }

        GameObject prefab = type == PlayerType.Killer ? killerPrefab : escapistPrefab;
        Vector3 spawnPos = GetSpawnPosition(type, escapistIndex);
        
        float rotationY = type == PlayerType.Killer ? killerRotationY : escapistRotationY;
        Quaternion spawnRotation = Quaternion.Euler(0f, rotationY, 0f);

        Transform parentTransform = type == PlayerType.Killer ? killerSpawnParent : escapistSpawnParent;

        if (parentTransform == null)
        {
            Debug.LogError($"[SpawnManager] ❌ SpawnParent NULL para tipo {type}");
            return;
        }

        GameObject playerGO = GameObject.Instantiate(prefab, spawnPos, spawnRotation, parentTransform);
        playerGO.name = $"{type}_{id}";

        var rb = playerGO.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
        }
        else
        {
            Debug.LogWarning($"[SpawnManager] ⚠️ Player {id} no tiene Rigidbody");
        }

        var view = playerGO.GetComponent<PlayerView>();
        if (view != null)
        {
            bool playerIsLocal = id == localPlayerId;
            bool isHostPlayer = id == 1;
            view.Init(id, isHostPlayer);
            view.SetLocal(playerIsLocal);

            try
            {
                var camBootstrap = ModularLobbyBootstrap.Instance?.GetComponent<PlayerCameraBootstrap>();
                camBootstrap?.RegisterPlayerView(view);
            }
            catch { }
        }
        else
        {
            Debug.LogWarning($"[SpawnManager] PlayerView no encontrado en {playerGO.name}");
        }

        // ✅ El collider del player debe ser NORMAL (no trigger)
        // El player necesita colisionar con la física
        Collider playerCollider = playerGO.GetComponent<Collider>();
        if (playerCollider != null)
        {
            // Asegurar que NO es trigger (para física normal)
            playerCollider.isTrigger = false;
            Debug.Log($"[SpawnManager] ✅ Collider configurado para player {id} (física normal, NO trigger)");
        }
        else
        {
            Debug.LogWarning($"[SpawnManager] ⚠️ Player {id} no tiene Collider - asignar uno en el prefab");
        }

        // Adjuntar script de trigger
        // El PlayerWinTrigger detectará colisiones con objetos que SEAN triggers
        var winTrigger = playerGO.AddComponent<PlayerWinTrigger>();

        // Obtener servicios necesarios
        var netService = ModularLobbyBootstrap.Instance?.GetLobbyNetworkService();
        var lobbyManager = ModularLobbyBootstrap.Instance?.GetLobbyManager();
        bool isHost = id == 1; 
        bool triggerPlayerIsLocal = id == localPlayerId;

        winTrigger.Init(id, isHost, triggerPlayerIsLocal, netService, lobbyManager);

        players[id] = playerGO;
        playerTypes[id] = type;
    }

    public void RemovePlayer(int id)
    {
        if (!players.ContainsKey(id)) return;

        var playerType = playerTypes[id];
        Debug.Log($"[SpawnManager] 🗑️ Removiendo player {id} ({playerType})");

        GameObject.Destroy(players[id]);
        players.Remove(id);

        if (playerTypes.ContainsKey(id))
            playerTypes.Remove(id);
    }

    public GameObject GetPlayerGameObject(int id)
    {
        if (players.TryGetValue(id, out GameObject playerGO))
        {
            return playerGO;
        }

        return null;
    }

    private Vector3 GetSpawnPosition(PlayerType type, int index)
    {
        if (type == PlayerType.Killer)
        {
            return killerSpawnPosition;
        }

        return escapistBasePosition + new Vector3(index * escapistSpacing, 0f, 0f);
    }

    public void SpawnRemotePlayer(int id, PlayerType type)
    {
        if (players.ContainsKey(id)) return;

        AddPlayer(id, type);
    }

    public int GetKillerCount() => playerTypes.Values.Count(t => t == PlayerType.Killer);
    public int GetEscapistCount() => playerTypes.Values.Count(t => t == PlayerType.Escapist);

    public void LogPlayerCounts()
    {
        Debug.Log($"[SpawnManager] 📊 Jugadores activos - Killers: {GetKillerCount()}, Escapists: {GetEscapistCount()}");
    }
}