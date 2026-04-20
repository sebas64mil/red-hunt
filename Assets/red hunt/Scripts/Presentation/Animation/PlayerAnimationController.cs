using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Animator Parameters")]
    [SerializeField] private string movementParameterName = "Mov";
    [SerializeField] private string isDeadParameterName = "IsDead";
    [SerializeField] private string isAttackParameterName = "IsAttack";

    [Header("Configuration")]
    [SerializeField] private float movementThreshold = 0.1f;

    private int currentMovementState = 0;
    private bool isDead = false;
    private int playerId = -1;

    private int movementHash;
    private int isDeadHash;
    private int isAttackHash;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        if (animator != null)
        {
            movementHash = Animator.StringToHash(movementParameterName);
            isDeadHash = Animator.StringToHash(isDeadParameterName);
            isAttackHash = Animator.StringToHash(isAttackParameterName);
        }
        else
        {
            Debug.LogError("[PlayerAnimationController] Animator not found in GameObject");
        }

    }

    public void Init(int id)
    {
        playerId = id;
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
        }
    }


    public void SetDeadAnimation(bool dead)
    {
        if (animator == null) return;

        if (isDead != dead)
        {
            isDead = dead;
            animator.SetBool(isDeadHash, isDead);
        }
    }


    public void PlayAttackAnimation()
    {
        if (animator == null) return;

        animator.SetTrigger(isAttackHash);
    }

    public int GetCurrentMovementState() => currentMovementState;

}