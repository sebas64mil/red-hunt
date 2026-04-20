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

    }

    public void CollectClue()
    {
        if (isCollected)
        {
            Debug.LogWarning($"[ClueItemController] Clue {clueId} already collected");
            return;
        }

        isCollected = true;
        gameObject.SetActive(false);

    }

    public void ResetClue()
    {
        isCollected = false;
        gameObject.SetActive(true);

        if (clueCollider != null)
        {
            clueCollider.enabled = true;
        }

    }
}