using System;
using UnityEngine;

public class KillerAttack : MonoBehaviour
{
    [SerializeField] private int damagePerHit = 1;
    [SerializeField] private string attackTriggerTag = "Attack";
    [SerializeField] private float attackCooldown = 0.5f;

    private PlayerInputHandler inputHandler;
    private int playerId = -1;
    private bool isLocal = false;  // ⭐ Inicializado en false por defecto

    private float timeSinceLastAttack = 0f;

    // ⭐ Referencias de red
    private BroadcastService broadcastService;
    private IClient client;
    private PacketBuilder packetBuilder;
    private bool isHost = false;

    // ⭐ NUEVO: GameStateManager
    private GameStateManager gameStateManager;
    private PlayerAnimationController animationController;

    public event Action<int, int> OnAttackConnected;

    private void Awake()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
        gameStateManager = FindFirstObjectByType<GameStateManager>();
        animationController = GetComponent<PlayerAnimationController>();

        if (inputHandler == null)
        {
            Debug.LogWarning("[KillerAttack] ?? PlayerInputHandler no encontrado");
        }

        if (gameStateManager == null)
        {
            Debug.LogWarning("[KillerAttack] ⚠️ GameStateManager no encontrado en escena");
        }

        if (animationController == null)
        {
            Debug.LogWarning("[KillerAttack] ⚠️ PlayerAnimationController no encontrado");
        }
    }


    public void Init(int id, bool local)
    {
        playerId = id;
        isLocal = local;  // ⭐ ESTO DEBE SER TRUE SI ES EL KILLER LOCAL
        Debug.Log($"[KillerAttack] ✅ Inicializado para Killer {playerId} - isLocal: {isLocal}");
    }

    public void InitNetworkServices(BroadcastService broadcast, IClient clientInstance, PacketBuilder builder, bool hostFlag)
    {
        broadcastService = broadcast;
        client = clientInstance;
        packetBuilder = builder;
        isHost = hostFlag;
        Debug.Log($"[KillerAttack] ✅ Servicios de red inicializados - isHost: {isHost}, playerId: {playerId}");
    }

    private void OnEnable()
    {
        if (inputHandler != null)
        {
            inputHandler.OnAttack += HandleAttackInput;
            Debug.Log($"[KillerAttack] ✅ Evento de ataque suscrito (isLocal: {isLocal}, playerId: {playerId})");
        }

        timeSinceLastAttack = attackCooldown;
    }

    private void OnDisable()
    {
        if (inputHandler != null)
        {
            inputHandler.OnAttack -= HandleAttackInput;
        }
    }

    private void FixedUpdate()
    {
        timeSinceLastAttack += Time.fixedDeltaTime;
    }

    private void HandleAttackInput()
    {
        // ⭐ CRÍTICO: Solo procesar input si es LOCAL
        if (!isLocal)
        {
            Debug.Log($"[KillerAttack] ⏭️ Killer {playerId} remoto - ignorando input local");
            return;
        }

        if (timeSinceLastAttack < attackCooldown)
        {
            Debug.Log($"[KillerAttack] ⏳ En cooldown ({timeSinceLastAttack:F2}/{attackCooldown})");
            return;
        }

        PerformAttack();
        timeSinceLastAttack = 0f;
    }

    private void PerformAttack()
    {
        Collider[] collidersInRange = Physics.OverlapSphere(transform.position, 2f);

        foreach (Collider collider in collidersInRange)
        {
            if (collider.CompareTag(attackTriggerTag))
            {
                var targetHealth = collider.GetComponent<EscapistHealth>();
                if (targetHealth != null && targetHealth.IsAlive)
                {
                    int targetPlayerId = GetPlayerIdFromGameObject(collider.gameObject);
                    
                    targetHealth.TakeDamage(damagePerHit);

                    Debug.Log($"[KillerAttack] ✅ Ataque conectado - Killer {playerId} → Escapista {targetPlayerId}");

                    // Llamar al controlador de animaciones
                    if (animationController != null)
                    {
                        animationController.PlayAttackAnimation();
                    }

                    OnAttackConnected?.Invoke(playerId, targetPlayerId);

                    SendHealthUpdateToNetwork(targetPlayerId, targetHealth.CurrentHealth, targetHealth.MaxHealth);
                }
            }
        }
    }


    private void SendHealthUpdateToNetwork(int targetPlayerId, int currentHealth, int maxHealth)
    {
        if (packetBuilder == null)
        {
            Debug.LogWarning("[KillerAttack] ⚠️ PacketBuilder no inicializado");
            return;
        }

        try
        {
            string healthUpdateJson = packetBuilder.CreateHealthUpdate(targetPlayerId, currentHealth, maxHealth);

            if (isHost && broadcastService != null)
            {
                _ = broadcastService.SendToAll(healthUpdateJson);
                Debug.Log($"[KillerAttack] 📡 HOST enviando HEALTH_UPDATE a TODOS - target: {targetPlayerId}, health: {currentHealth}/{maxHealth}");
            }
            else if (!isHost && client != null && client.isConnected)
            {
                _ = client.SendMessageAsync(healthUpdateJson);
                Debug.Log($"[KillerAttack] 📤 CLIENTE enviando HEALTH_UPDATE AL HOST - target: {targetPlayerId}, health: {currentHealth}/{maxHealth}");
            }
            else
            {
                Debug.LogWarning($"[KillerAttack] ⚠️ Sin servicios de red - isHost: {isHost}, broadcast: {(broadcastService != null ? "✅" : "❌")}, client: {(client != null ? "✅" : "❌")}")
;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KillerAttack] ❌ Error en SendHealthUpdateToNetwork: {ex.Message}");
        }
    }

    private int GetPlayerIdFromGameObject(GameObject targetGO)
    {
        var playerView = targetGO.GetComponent<PlayerView>();
        if (playerView != null)
        {
            return playerView.PlayerId;
        }

        var parentPlayerView = targetGO.GetComponentInParent<PlayerView>();
        if (parentPlayerView != null)
        {
            return parentPlayerView.PlayerId;
        }

        return -1;
    }

    public float GetAttackCooldownRemaining()
    {
        return Mathf.Max(0, attackCooldown - timeSinceLastAttack);
    }
}