using UnityEngine;

public class ClueItemController : MonoBehaviour
{
    [SerializeField] private string clueId;
    private Collider clueCollider;
    private bool isCollected = false;

    public string ClueId => clueId;
    public bool IsCollected => isCollected;

    private void Awake()
    {
        clueCollider = GetComponent<Collider>();

        if (string.IsNullOrEmpty(clueId))
        {
            clueId = gameObject.name;
        }

        Debug.Log($"[ClueItemController] Pista inicializada: {clueId}");
    }

    public void CollectClue()
    {
        if (isCollected)
        {
            Debug.LogWarning($"[ClueItemController] Pista {clueId} ya fue recolectada");
            return;
        }

        isCollected = true;
        gameObject.SetActive(false);

        Debug.Log($"[ClueItemController] ✅ Pista {clueId} recolectada y desactivada");
    }

    public void ResetClue()
    {
        isCollected = false;
        gameObject.SetActive(true);

        if (clueCollider != null)
        {
            clueCollider.enabled = true;
        }

        Debug.Log($"[ClueItemController] 🔄 Pista {clueId} reseteada");
    }
}