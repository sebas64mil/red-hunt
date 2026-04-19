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

        Debug.Log($"[DoorController] ✅ Inicializado - requiere {requiredClues} pistas");
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
            Debug.LogWarning("[DoorController] ⚠️ No se puede abrir la puerta: Animator no asignado");
            return;
        }

        if (isOpenParameterHash == 0)
        {
            Debug.LogWarning("[DoorController] ⚠️ No se puede abrir la puerta: parámetro IsOpen no configurado");
            return;
        }

        doorAnimator.gameObject.SetActive(false);
        // doorAnimator.SetBool(isOpenParameterHash, true);

        Debug.Log($"[DoorController] 🔓 Puerta desbloqueada - {requiredClues} pistas (GLOBAL) alcanzadas");
    }

    public void ResetDoor()
    {
        isDoorUnlocked = false;

        if (doorAnimator != null && isOpenParameterHash != 0)
        {
            doorAnimator.SetBool(isOpenParameterName, false);
        }

        Debug.Log("[DoorController] 🔒 Puerta reseteada");
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