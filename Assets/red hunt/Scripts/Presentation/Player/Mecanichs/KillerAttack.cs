using System;
using UnityEngine;

public class KillerAttack : MonoBehaviour
{
    [SerializeField] private int damagePerHit = 1;
    [SerializeField] private string attackTriggerTag = "Attack";
    [SerializeField] private float attackCooldown = 0.5f;

    private PlayerInputHandler inputHandler;
    private int playerId = -1;
    private bool isLocal = false;

    private float timeSinceLastAttack = 0f;

    private BroadcastService broadcastService;
    private IClient client;
    private PacketBuilder packetBuilder;
    private bool isHost = false;

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
            Debug.LogWarning("[KillerAttack] PlayerInputHandler not found");
        }

        if (gameStateManager == null)
        {
            Debug.LogWarning("[KillerAttack] GameStateManager not found in scene");
        }

        if (animationController == null)
        {
            Debug.LogWarning("[KillerAttack] PlayerAnimationController not found");
        }
    }


    public void Init(int id, bool local)
    {
        playerId = id;
        isLocal = local;
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
            inputHandler.OnAttack += HandleAttackInput;
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
        if (!isLocal)
        {
            return;
        }

        if (timeSinceLastAttack < attackCooldown)
        {
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
            Debug.LogWarning("[KillerAttack] PacketBuilder not initialized");
            return;
        }

        try
        {
            string healthUpdateJson = packetBuilder.CreateHealthUpdate(targetPlayerId, currentHealth, maxHealth);

            if (isHost && broadcastService != null)
            {
                _ = broadcastService.SendToAll(healthUpdateJson);
            }
            else if (!isHost && client != null && client.isConnected)
            {
                _ = client.SendMessageAsync(healthUpdateJson);
            }
            else
            {
                Debug.LogWarning($"[KillerAttack] No network services - isHost: {isHost}, broadcast: {(broadcastService != null ? "available" : "unavailable")}, client: {(client != null ? "available" : "unavailable")}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KillerAttack] Error in SendHealthUpdateToNetwork: {ex.Message}");
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