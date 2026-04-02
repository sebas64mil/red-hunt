using System;
using UnityEngine;

public class PlayerNetworkService : MonoBehaviour
{
    [SerializeField] private float syncRate = 0.1f;
    [SerializeField] private float connectionCheckInterval = 0.5f;
    [SerializeField] private float positionThreshold = 0.01f;        // ⭐ Umbral mínimo de cambio de posición
    [SerializeField] private float rotationThreshold = 0.5f;         // ⭐ Umbral mínimo de cambio de rotación (grados)

    private IClient client;
    private BroadcastService broadcastService;
    private PlayerPacketBuilder playerPacketBuilder;
    private int playerId = -1;
    private bool isHost = false;

    private PlayerMovement playerMovement;
    private Transform playerTransform;

    // ⭐ NUEVO: Cache de última posición/rotación enviada
    private Vector3 lastSentPosition = Vector3.zero;
    private Quaternion lastSentRotation = Quaternion.identity;
    private Vector3 lastSentVelocity = Vector3.zero;
    private bool lastSentIsJumping = false;

    private float timeSinceLastSync = 0f;
    private float timeSinceLastConnectionCheck = 0f;
    private bool connectionReady = false;

    public event Action<MovePacket> OnRemotePlayerMove;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerTransform = transform;
    }

    public void Init(int id, IClient clientInstance, ISerializer serializer, bool hostFlag, BroadcastService broadcastServiceInstance = null)
    {
        playerId = id;
        client = clientInstance;
        broadcastService = broadcastServiceInstance;
        playerPacketBuilder = new PlayerPacketBuilder(serializer ?? throw new ArgumentNullException(nameof(serializer)));
        isHost = hostFlag;

        // ⭐ Inicializar cache con valores actuales
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

        timeSinceLastSync += Time.fixedDeltaTime;

        if (timeSinceLastSync >= syncRate)
        {
            SendLocalPlayerPosition();
            timeSinceLastSync = 0f;
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

    private void SendLocalPlayerPosition()
    {
        if (playerMovement == null || playerTransform == null) return;

        // ⭐ NUEVO: Verificar si hay cambios significativos
        if (!HasSignificantChange())
        {
            return; // No enviar si no hay cambios
        }

        try
        {
            string movePacketJson = playerPacketBuilder.CreateMovePacket(
                playerId,
                playerTransform,
                playerMovement.CurrentVelocity,
                playerMovement.IsJumping
            );

            // ⭐ Actualizar cache después de crear el packet
            lastSentPosition = playerTransform.position;
            lastSentRotation = playerTransform.rotation;
            lastSentVelocity = playerMovement.CurrentVelocity;
            lastSentIsJumping = playerMovement.IsJumping;

            if (isHost && broadcastService != null)
            {
                _ = broadcastService.SendToAll(movePacketJson);
                Debug.Log($"[PlayerNetworkService] 📤 Host enviando MOVE con cambio: pos={playerTransform.position}");
            }
            else if (!isHost && client != null && client.isConnected)
            {
                _ = client.SendMessageAsync(movePacketJson);
                Debug.Log($"[PlayerNetworkService] 📤 Cliente {playerId} enviando MOVE con cambio: pos={playerTransform.position}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerNetworkService] Error enviando posición: {ex.Message}");
        }
    }

    // ⭐ NUEVO: Método para detectar cambios significativos
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