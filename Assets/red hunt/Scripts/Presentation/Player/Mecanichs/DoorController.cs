using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("Clues")]
    [SerializeField] private int requiredClues = 4;

    [Header("Door Animation")]
    [SerializeField] private Animator doorAnimator;

    [SerializeField] private string isOpenParameterName = "IsOpen";

    private int isOpenParameterHash;

    private EscapistClueRegistry clueRegistry;
    private bool isDoorUnlocked = false;

    public void Init(EscapistClueRegistry registry)
    {
        clueRegistry = registry;
        EnsureAnimator();
        CacheAnimatorHashes();

    }

    private void Awake()
    {
        EnsureAnimator();
        CacheAnimatorHashes();
    }

    private void Update()
    {
        if (isDoorUnlocked || clueRegistry == null)
        {
            return;
        }

        if (clueRegistry.HaveAllRequiredClues(requiredClues))
        {
            UnlockDoor();
        }
    }

    private void UnlockDoor()
    {
        isDoorUnlocked = true;

        if (doorAnimator == null)
        {
            Debug.LogWarning("[DoorController] Cannot open door: Animator not assigned");
            return;
        }

        if (isOpenParameterHash == 0)
        {
            Debug.LogWarning("[DoorController] Cannot open door: IsOpen parameter not configured");
            return;
        }

        doorAnimator.gameObject.SetActive(false);

    }

    public void ResetDoor()
    {
        isDoorUnlocked = false;

        if (doorAnimator != null && isOpenParameterHash != 0)
        {
            doorAnimator.SetBool(isOpenParameterName, false);
        }

    }

    private void EnsureAnimator()
    {
        if (doorAnimator != null) return;

        doorAnimator = GetComponent<Animator>();
        if (doorAnimator == null)
        {
            doorAnimator = GetComponentInChildren<Animator>();
        }
    }

    private void CacheAnimatorHashes()
    {
        isOpenParameterHash = string.IsNullOrWhiteSpace(isOpenParameterName)
            ? 0
            : Animator.StringToHash(isOpenParameterName);
    }
}