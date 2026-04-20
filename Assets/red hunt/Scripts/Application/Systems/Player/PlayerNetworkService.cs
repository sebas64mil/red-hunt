using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerNetworkService : MonoBehaviour
{
    [Header("Synchronization")]
    [SerializeField] private float syncRate = 0.1f;
    [SerializeField] private float snapshotRate = 0.1f;
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

    private Vector3 lastSentPosition = Vector3.zero;
    private Quaternion lastSentRotation = Quaternion.identity;
    private Vector3 lastSentVelocity = Vector3.zero;
    private bool lastSentIsJumping = false;

    private float timeSinceLastSync = 0f;
    private float timeSinceLastSnapshot = 0f;
    private float timeSinceLastConnectionCheck = 0f;
    private bool connectionReady = false;

    private SpawnManager spawnManager;
    private LobbyManager lobbyManager;


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
        SpawnManager spawnManagerInstance = null,
        LobbyManager lobbyManagerInstance = null)
    {
        playerId = id;
        client = clientInstance;
        broadcastService = broadcastServiceInstance;
        playerPacketBuilder = new PlayerPacketBuilder(serializer ?? throw new ArgumentNullException(nameof(serializer)));
        isHost = hostFlag;
        spawnManager = spawnManagerInstance;
        lobbyManager = lobbyManagerInstance;

        if (playerTransform != null)
        {
            lastSentPosition = playerTransform.position;
            lastSentRotation = playerTransform.rotation;
        }

        UpdateConnectionStatus();

    }

    private void OnEnable()
    {
        timeSinceLastSync = 0f;
        timeSinceLastSnapshot = 0f;
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

        if (isHost)
        {
            timeSinceLastSnapshot += Time.fixedDeltaTime;
            if (timeSinceLastSnapshot >= snapshotRate)
            {
                SendPlayerStateSnapshot();
                timeSinceLastSnapshot = 0f;
            }
        }
        else
        {
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
                Debug.LogWarning($"[PlayerNetworkService] Host {playerId} without BroadcastService. Status: {connectionReady}");
            }
        }
        else
        {
            connectionReady = client != null && client.isConnected;
            if (!connectionReady)
            {
                Debug.LogWarning($"[PlayerNetworkService] Client {playerId} not connected. Status: {client?.isConnected ?? false}");
            }
        }
    }

    private void SendPlayerStateSnapshot()
    {
        if (lobbyManager == null || spawnManager == null || broadcastService == null)
        {
            Debug.LogWarning("[PlayerNetworkService] Cannot send snapshot: lobbyManager, spawnManager or broadcastService is NULL");
            return;
        }

        try
        {
            var playersData = new Dictionary<int, (Transform transform, Vector3 velocity, bool isJumping)>();

            var allPlayers = lobbyManager.GetAllPlayers();
            foreach (var playerSession in allPlayers)
            {
                var playerGO = spawnManager.GetPlayerGameObject(playerSession.Id);
                if (playerGO == null)
                {
                    Debug.LogWarning($"[PlayerNetworkService] Player GameObject not found for {playerSession.Id}");
                    continue;
                }

                var playerMovementComponent = playerGO.GetComponent<PlayerMovement>();
                if (playerMovementComponent == null)
                {
                    Debug.LogWarning($"[PlayerNetworkService] PlayerMovement not found for {playerSession.Id}");
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
                Debug.LogWarning("[PlayerNetworkService] No player data available for snapshot");
                return;
            }

            string snapshotJson = playerPacketBuilder.CreatePlayerStateSnapshot(playersData);
            
            _ = broadcastService.SendToAll(snapshotJson);
            
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerNetworkService] Error sending snapshot: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void SendLocalPlayerPosition()
    {
        if (playerMovement == null || playerTransform == null) return;

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

            lastSentPosition = playerTransform.position;
            lastSentRotation = playerTransform.rotation;
            lastSentVelocity = playerMovement.CurrentVelocity;
            lastSentIsJumping = playerMovement.IsJumping;

            if (!isHost && client != null && client.isConnected)
            {
                _ = client.SendMessageAsync(movePacketJson);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerNetworkService] Error sending position: {ex.Message}");
        }
    }

    private bool HasSignificantChange()
    {
        Vector3 currentPos = playerTransform.position;
        Quaternion currentRot = playerTransform.rotation;
        Vector3 currentVel = playerMovement.CurrentVelocity;
        bool currentIsJumping = playerMovement.IsJumping;

        float positionDistance = Vector3.Distance(currentPos, lastSentPosition);
        if (positionDistance > positionThreshold)
        {
            return true;
        }

        float rotationAngle = Quaternion.Angle(currentRot, lastSentRotation);
        if (rotationAngle > rotationThreshold)
        {
            return true;
        }

        float velocityChange = Vector3.Distance(currentVel, lastSentVelocity);
        if (velocityChange > 0.1f)
        {
            return true;
        }

        if (currentIsJumping != lastSentIsJumping)
        {
            return true;
        }

        return false;
    }


    public bool IsInitialized() => (isHost ? broadcastService != null : client != null) && playerPacketBuilder != null;

}