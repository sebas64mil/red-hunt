using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Parámetros del Animator")]
    [SerializeField] private string movementParameterName = "Mov";
    [SerializeField] private string isDeadParameterName = "IsDead";
    [SerializeField] private string isAttackParameterName = "IsAttack";

    [Header("Configuración")]
    [SerializeField] private float movementThreshold = 0.1f;

    // Estado
    private int currentMovementState = 0;  // 0 = Idle, 1 = Walk
    private bool isDead = false;
    private int playerId = -1;

    // Hash de parámetros (optimización)
    private int movementHash;
    private int isDeadHash;
    private int isAttackHash;

    private void Awake()
    {
        // Obtener Animator si no está asignado
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        // Cachear hashes de parámetros
        if (animator != null)
        {
            movementHash = Animator.StringToHash(movementParameterName);
            isDeadHash = Animator.StringToHash(isDeadParameterName);
            isAttackHash = Animator.StringToHash(isAttackParameterName);
            Debug.Log("[PlayerAnimationController] ✅ Hashes de parámetros cacheados");
        }
        else
        {
            Debug.LogError("[PlayerAnimationController] ❌ Animator no encontrado en el GameObject");
        }

        Debug.Log("[PlayerAnimationController] ✅ Inicializado correctamente");
    }

    public void Init(int id)
    {
        playerId = id;
        Debug.Log($"[PlayerAnimationController] ✅ Inicializado para player {id}");
    }


    public void UpdateMovementAnimation(Vector3 velocity)
    {
        if (animator == null) return;

        float movementSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        int newMovementState = movementSpeed > movementThreshold ? 1 : 0;

        if (currentMovementState != newMovementState)
        {
            currentMovementState = newMovementState;
            animator.SetFloat(movementHash, currentMovementState);

            string stateName = currentMovementState == 1 ? "Walk" : "Idle";
            Debug.Log($"[PlayerAnimationController] 🎬 Movimiento: {stateName} (velocidad: {movementSpeed:F2})");
        }
    }


    public void SetDeadAnimation(bool dead)
    {
        if (animator == null) return;

        if (isDead != dead)
        {
            isDead = dead;
            animator.SetBool(isDeadHash, isDead);
            Debug.Log($"[PlayerAnimationController] 💀 Estado de muerte: {isDead}");
        }
    }


    public void PlayAttackAnimation()
    {
        if (animator == null) return;

        animator.SetTrigger(isAttackHash);
        Debug.Log($"[PlayerAnimationController] ⚔️ Trigger de ataque activado");
    }

    public int GetCurrentMovementState() => currentMovementState;

    public bool GetIsDead() => isDead;
}