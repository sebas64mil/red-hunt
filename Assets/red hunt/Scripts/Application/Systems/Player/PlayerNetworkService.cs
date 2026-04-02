using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerNetworkService : MonoBehaviour
{
    [Header("Sincronización")]
    [SerializeField] private float syncRate = 0.1f;
    [SerializeField] private float snapshotRate = 0.1f;  // ⭐ NUEVO: Snapshot cada 100ms
    [SerializeField] private float connectionCheckInterval = 0.5f;
    [SerializeField] private float positionThreshold = 0.01f;
    [SerializeField] private float rotationThreshold = 0.5f;

    private IClient client;
    private BroadcastService broadcastService;
    private PlayerPacketBuilder playerPacketBuilder;
    private int playerId = -1;
    private bool isHost = false;

    private PlayerMovement playerMovement;
    private Transform playerTransform;

    // ⭐ Cache de última posición/rotación enviada
    private Vector3 lastSentPosition = Vector3.zero;
    private Quaternion lastSentRotation = Quaternion.identity;
    private Vector3 lastSentVelocity = Vector3.zero;
    private bool lastSentIsJumping = false;

    private float timeSinceLastSync = 0f;
    private float timeSinceLastSnapshot = 0f;  // ⭐ NUEVO
    private float timeSinceLastConnectionCheck = 0f;
    private bool connectionReady = false;

    // ⭐ NUEVO: Referencia al SpawnManager para acceder a todos los jugadores
    private SpawnManager spawnManager;
    private LobbyManager lobbyManager;

    public event Action<MovePacket> OnRemotePlayerMove;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerTransform = transform;
    }

    public void Init(
        int id, 
        IClient clientInstance, 
        ISerializer serializer, 
        bool hostFlag, 
        BroadcastService broadcastServiceInstance = null,
        SpawnManager spawnManagerInstance = null,  // ⭐ NUEVO
        LobbyManager lobbyManagerInstance = null)   // ⭐ NUEVO
    {
        playerId = id;
        client = clientInstance;
        broadcastService = broadcastServiceInstance;
        playerPacketBuilder = new PlayerPacketBuilder(serializer ?? throw new ArgumentNullException(nameof(serializer)));
        isHost = hostFlag;
        spawnManager = spawnManagerInstance;  // ⭐ NUEVO
        lobbyManager = lobbyManagerInstance;   // ⭐ NUEVO

        // Inicializar cache con valores actuales
        if (playerTransform != null)
        {
            lastSentPosition = playerTransform.position;
            lastSentRotation = playerTransform.rotation;
        }

        UpdateConnectionStatus();

        Debug.Log($"[PlayerNetworkService] Inicializado para PlayerId {playerId} (Host: {isHost}, Conectado: {connectionReady})");
    }

    private void OnEnable()
    {
        timeSinceLastSync = 0f;
        timeSinceLastSnapshot = 0f;  // ⭐ NUEVO
        timeSinceLastConnectionCheck = 0f;
    }

    private void FixedUpdate()
    {
        if (!IsInitialized() || playerId < 0) return;

        timeSinceLastConnectionCheck += Time.fixedDeltaTime;
        if (timeSinceLastConnectionCheck >= connectionCheckInterval)
        {
            UpdateConnectionStatus();
            timeSinceLastConnectionCheck = 0f;
        }

        if (!connectionReady)
        {
            return;
        }

        // ⭐ NUEVO: Lógica diferente para host y cliente
        if (isHost)
        {
            // HOST: Enviar snapshot de todos los jugadores
            timeSinceLastSnapshot += Time.fixedDeltaTime;
            if (timeSinceLastSnapshot >= snapshotRate)
            {
                SendPlayerStateSnapshot();
                timeSinceLastSnapshot = 0f;
            }
        }
        else
        {
            // CLIENTE: Enviar solo su MOVE
            timeSinceLastSync += Time.fixedDeltaTime;
            if (timeSinceLastSync >= syncRate)
            {
                SendLocalPlayerPosition();
                timeSinceLastSync = 0f;
            }
        }
    }

    private void UpdateConnectionStatus()
    {
        if (isHost)
        {
            connectionReady = broadcastService != null;
            if (!connectionReady)
            {
                Debug.LogWarning($"[PlayerNetworkService] ⚠️ Host {playerId} sin BroadcastService. Estado: {connectionReady}");
            }
        }
        else
        {
            connectionReady = client != null && client.isConnected;
            if (!connectionReady)
            {
                Debug.LogWarning($"[PlayerNetworkService] ⚠️ Cliente {playerId} no conectado. Estado: {client?.isConnected ?? false}");
            }
        }
    }

    // ⭐ NUEVO: Enviar snapshot de TODOS los jugadores (solo host)
    private void SendPlayerStateSnapshot()
    {
        if (lobbyManager == null || spawnManager == null || broadcastService == null)
        {
            Debug.LogWarning("[PlayerNetworkService] ❌ No se puede enviar snapshot: lobbyManager, spawnManager o broadcastService es NULL");
            return;
        }

        try
        {
            var playersData = new Dictionary<int, (Transform transform, Vector3 velocity, bool isJumping)>();

            // Recolectar datos de TODOS los jugadores
            var allPlayers = lobbyManager.GetAllPlayers();
            foreach (var playerSession in allPlayers)
            {
                var playerGO = spawnManager.GetPlayerGameObject(playerSession.Id);
                if (playerGO == null)
                {
                    Debug.LogWarning($"[PlayerNetworkService] ⚠️ Player GameObject no encontrado para {playerSession.Id}");
                    continue;
                }

                var playerMovementComponent = playerGO.GetComponent<PlayerMovement>();
                if (playerMovementComponent == null)
                {
                    Debug.LogWarning($"[PlayerNetworkService] ⚠️ PlayerMovement no encontrado para {playerSession.Id}");
                    continue;
                }

                playersData[playerSession.Id] = (
                    playerGO.transform,
                    playerMovementComponent.CurrentVelocity,
                    playerMovementComponent.IsJumping
                );
            }

            if (playersData.Count == 0)
            {
                Debug.LogWarning("[PlayerNetworkService] ⚠️ No hay datos de jugadores para el snapshot");
                return;
            }

            string snapshotJson = playerPacketBuilder.CreatePlayerStateSnapshot(playersData);
            
            _ = broadcastService.SendToAll(snapshotJson);
            
            Debug.Log($"[PlayerNetworkService] 📸 Snapshot HOST enviado: {playersData.Count} jugadores");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerNetworkService] ❌ Error enviando snapshot: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // ⭐ ORIGINAL: Enviar solo el MOVE del jugador local (cliente)
    private void SendLocalPlayerPosition()
    {
        if (playerMovement == null || playerTransform == null) return;

        // Verificar si hay cambios significativos
        if (!HasSignificantChange())
        {
            return;
        }

        try
        {
            string movePacketJson = playerPacketBuilder.CreateMovePacket(
                playerId,
                playerTransform,
                playerMovement.CurrentVelocity,
                playerMovement.IsJumping
            );

            // Actualizar cache después de crear el packet
            lastSentPosition = playerTransform.position;
            lastSentRotation = playerTransform.rotation;
            lastSentVelocity = playerMovement.CurrentVelocity;
            lastSentIsJumping = playerMovement.IsJumping;

            if (!isHost && client != null && client.isConnected)
            {
                _ = client.SendMessageAsync(movePacketJson);
                Debug.Log($"[PlayerNetworkService] 📤 Cliente {playerId} enviando MOVE: pos={playerTransform.position}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerNetworkService] Error enviando posición: {ex.Message}");
        }
    }

    private bool HasSignificantChange()
    {
        Vector3 currentPos = playerTransform.position;
        Quaternion currentRot = playerTransform.rotation;
        Vector3 currentVel = playerMovement.CurrentVelocity;
        bool currentIsJumping = playerMovement.IsJumping;

        // Cambio de posición
        float positionDistance = Vector3.Distance(currentPos, lastSentPosition);
        if (positionDistance > positionThreshold)
        {
            return true;
        }

        // Cambio de rotación (en grados)
        float rotationAngle = Quaternion.Angle(currentRot, lastSentRotation);
        if (rotationAngle > rotationThreshold)
        {
            return true;
        }

        // Cambio en velocidad
        float velocityChange = Vector3.Distance(currentVel, lastSentVelocity);
        if (velocityChange > 0.1f)
        {
            return true;
        }

        // Cambio en estado de salto
        if (currentIsJumping != lastSentIsJumping)
        {
            return true;
        }

        return false;
    }

    public void HandleRemotePlayerMove(string movePacketJson)
    {
        if (!IsInitialized()) return;

        try
        {
            var movePacket = playerPacketBuilder.DeserializeMovePacket(movePacketJson);
            if (movePacket == null) return;

            OnRemotePlayerMove?.Invoke(movePacket);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerNetworkService] Error procesando posición remota: {ex.Message}");
        }
    }

    public bool IsInitialized() => (isHost ? broadcastService != null : client != null) && playerPacketBuilder != null;

    public int GetPlayerId() => playerId;
    public bool IsHost() => isHost;
}