using UnityEngine;

public class DoorController : MonoBehaviour
{
    [SerializeField] private int requiredClues = 4;

    private EscapistClueRegistry clueRegistry;
    private bool isDoorUnlocked = false;

    public void Init(EscapistClueRegistry registry)
    {
        clueRegistry = registry;
        Debug.Log($"[DoorController] ✅ Inicializado - requiere {requiredClues} pistas");
    }

    private void Update()
    {
        if (isDoorUnlocked || clueRegistry == null)
        {
            return;
        }

        // Verificar si se han recolectado todas las pistas
        if (clueRegistry.HaveAllRequiredClues(requiredClues))
        {
            UnlockDoor();
        }
    }

    private void UnlockDoor()
    {
        isDoorUnlocked = true;
        gameObject.SetActive(false);

        Debug.Log($"[DoorController] 🔓 Puerta desbloqueada - todas las {requiredClues} pistas recolectadas");
    }

    public void ResetDoor()
    {
        isDoorUnlocked = false;
        gameObject.SetActive(true);
        Debug.Log($"[DoorController] 🔒 Puerta reseteada");
    }
}