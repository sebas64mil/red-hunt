using System;
using UnityEngine;

public class ClueCollector : MonoBehaviour
{
    [SerializeField] private string clueTag = "Clue";

    private PlayerInputHandler inputHandler;
    private int playerId = -1;
    private bool isLocal = false;

    private BroadcastService broadcastService;
    private IClient client;
    private PacketBuilder packetBuilder;
    private bool isHost = false;

    private EscapistClueRegistry clueRegistry;

    private GameObject nearbyClue;
    private ClueItemController nearbyClueController;

    public event Action<int, string> OnClueCollected;

    private void Awake()
    {
        inputHandler = GetComponent<PlayerInputHandler>();

        if (inputHandler == null)
        {
            Debug.LogWarning("[ClueCollector] PlayerInputHandler not found");
        }
    }

    public void Init(int id, bool local, EscapistClueRegistry registry)
    {
        playerId = id;
        isLocal = local;
        clueRegistry = registry;
    }

    public void InitNetworkServices(BroadcastService broadcast, IClient clientInstance, PacketBuilder builder, bool hostFlag)
    {
        broadcastService = broadcast;
        client = clientInstance;
        packetBuilder = builder;
        isHost = hostFlag;
    }

    private void OnEnable()
    {
        if (inputHandler != null)
        {
            inputHandler.OnInteract += HandleInteractInput;
        }
    }

    private void OnDisable()
    {
        if (inputHandler != null)
        {
            inputHandler.OnInteract -= HandleInteractInput;
        }
        
        nearbyClue = null;
        nearbyClueController = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(clueTag))
        {
            var clueController = other.GetComponent<ClueItemController>();
            if (clueController != null && !clueController.IsCollected)
            {
                nearbyClue = other.gameObject;
                nearbyClueController = clueController;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(clueTag) && other.gameObject == nearbyClue)
        {
            nearbyClue = null;
            nearbyClueController = null;
        }
    }

    private void HandleInteractInput()
    {
        if (!isLocal)
        {
            return;
        }

        if (nearbyClue == null || nearbyClueController == null)
        {
            return;
        }

        string clueId = nearbyClueController.ClueId;
        CollectClue(clueId, nearbyClueController);
    }

    private void CollectClue(string clueId, ClueItemController clueController)
    {

        clueController.CollectClue();

        if (clueRegistry != null)
        {
            clueRegistry.AddClue(playerId, clueId);
        }

        OnClueCollected?.Invoke(playerId, clueId);

        SendClueCollectedToNetwork(clueId);

        nearbyClue = null;
        nearbyClueController = null;
    }

    private void SendClueCollectedToNetwork(string clueId)
    {
        if (packetBuilder == null)
        {
            Debug.LogWarning("[ClueCollector] PacketBuilder not initialized");
            return;
        }

        try
        {
            string cluePacketJson = packetBuilder.CreateEscapistClueCollected(playerId, clueId);

            if (isHost && broadcastService != null)
            {
                _ = broadcastService.SendToAll(cluePacketJson);
            }
            else if (!isHost && client != null && client.isConnected)
            {
                _ = client.SendMessageAsync(cluePacketJson);
            }
            else
            {
                Debug.LogWarning($"[ClueCollector] No network services - isHost: {isHost}, broadcast: {(broadcastService != null ? "available" : "unavailable")}, client: {(client != null ? "available" : "unavailable")}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClueCollector] Error in SendClueCollectedToNetwork: {ex.Message}");
        }
    }

    public void OnClueCollectedFromNetwork(string clueId)
    {
        if (clueRegistry != null)
        {
            clueRegistry.AddClue(playerId, clueId);
        }
    }
}