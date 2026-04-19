using System;
using UnityEngine;

public class ClueCollector : MonoBehaviour
{
    [SerializeField] private string clueTag = "Clue";

    private PlayerInputHandler inputHandler;
    private int playerId = -1;
    private bool isLocal = false;

    // Referencias de red
    private BroadcastService broadcastService;
    private IClient client;
    private PacketBuilder packetBuilder;
    private bool isHost = false;

    // Registro local de pistas recolectadas
    private EscapistClueRegistry clueRegistry;

    // Pista cercana que puede ser recolectada
    private GameObject nearbyClue;
    private ClueItemController nearbyClueController;

    public event Action<int, string> OnClueCollected;

    private void Awake()
    {
        inputHandler = GetComponent<PlayerInputHandler>();

        if (inputHandler == null)
        {
            Debug.LogWarning("[ClueCollector] ⚠️ PlayerInputHandler no encontrado");
        }
    }

    public void Init(int id, bool local, EscapistClueRegistry registry)
    {
        playerId = id;
        isLocal = local;
        clueRegistry = registry;
        Debug.Log($"[ClueCollector] ✅ Inicializado para Escapist {playerId} - isLocal: {isLocal}");
    }

    public void InitNetworkServices(BroadcastService broadcast, IClient clientInstance, PacketBuilder builder, bool hostFlag)
    {
        broadcastService = broadcast;
        client = clientInstance;
        packetBuilder = builder;
        isHost = hostFlag;
        Debug.Log($"[ClueCollector] ✅ Servicios de red inicializados - isHost: {isHost}, playerId: {playerId}");
    }

    private void OnEnable()
    {
        if (inputHandler != null)
        {
            inputHandler.OnInteract += HandleInteractInput;
            Debug.Log($"[ClueCollector] ✅ Evento de interacción suscrito (isLocal: {isLocal}, playerId: {playerId})");
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
                Debug.Log($"[ClueCollector] 📍 Pista detectada cerca: {clueController.ClueId}");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(clueTag) && other.gameObject == nearbyClue)
        {
            nearbyClue = null;
            nearbyClueController = null;
            Debug.Log($"[ClueCollector] 📍 Saliste del rango de la pista");
        }
    }

    private void HandleInteractInput()
    {
        if (!isLocal)
        {
            Debug.Log($"[ClueCollector] ⏭️ Escapist {playerId} remoto - ignorando input local");
            return;
        }

        if (nearbyClue == null || nearbyClueController == null)
        {
            Debug.Log("[ClueCollector] ℹ️ No hay pistas cercanas para recolectar");
            return;
        }

        string clueId = nearbyClueController.ClueId;
        CollectClue(clueId, nearbyClueController);
    }

    private void CollectClue(string clueId, ClueItemController clueController)
    {
        Debug.Log($"[ClueCollector] 🔑 Recolectando pista: {clueId} para Escapist {playerId}");

        // Desactivar pista localmente
        clueController.CollectClue();

        // Registrar en el registro local
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
            Debug.LogWarning("[ClueCollector] ⚠️ PacketBuilder no inicializado");
            return;
        }

        try
        {
            string cluePacketJson = packetBuilder.CreateEscapistClueCollected(playerId, clueId);

            // ⭐ CORREGIDO: Usar isHost en lugar de isLocal para decidir si broadcast
            if (isHost && broadcastService != null)
            {
                _ = broadcastService.SendToAll(cluePacketJson);
                Debug.Log($"[ClueCollector] 📡 HOST enviando ESCAPIST_CLUE_COLLECTED a TODOS - clueId: {clueId}");
            }
            else if (!isHost && client != null && client.isConnected)
            {
                _ = client.SendMessageAsync(cluePacketJson);
                Debug.Log($"[ClueCollector] 📤 CLIENTE enviando ESCAPIST_CLUE_COLLECTED AL HOST - clueId: {clueId}");
            }
            else
            {
                Debug.LogWarning($"[ClueCollector] ⚠️ Sin servicios de red - isHost: {isHost}, broadcast: {(broadcastService != null ? "✅" : "❌")}, client: {(client != null ? "✅" : "❌")}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClueCollector] ❌ Error en SendClueCollectedToNetwork: {ex.Message}");
        }
    }

    public void OnClueCollectedFromNetwork(string clueId)
    {
        if (clueRegistry != null)
        {
            clueRegistry.AddClue(playerId, clueId);
            Debug.Log($"[ClueCollector] 🔑 Pista recibida de la red: {clueId} para Escapist {playerId}");
        }
    }
}