using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EscapistCluesDisplay : MonoBehaviour
{
    [SerializeField] private Image mainClueImage;
    [SerializeField] private Sprite clue0Sprite;
    [SerializeField] private Sprite clue1Sprite;
    [SerializeField] private Sprite clue2Sprite;
    [SerializeField] private Sprite clue3Sprite;

    private EscapistClueRegistry clueRegistry;
    private int localPlayerId = -1;
    private bool initialized = false;

    public void Initialize(EscapistClueRegistry registry, int playerId)
    {
        clueRegistry = registry;
        localPlayerId = playerId;
        initialized = true;
    }

    public void HideForKiller()
    {
        if (mainClueImage != null)
        {
            mainClueImage.gameObject.SetActive(false);
        }
    }

    public void OnSnapshotReceived(IReadOnlyDictionary<int, IReadOnlyCollection<string>> cluesByEscapist)
    {
        if (!initialized || localPlayerId <= 0)
        {
            return;
        }

        
        if (clueRegistry != null)
        {
            clueRegistry.SyncFromSnapshot(cluesByEscapist);
        }
        else
        {
            Debug.LogError("[EscapistCluesDisplay] ClueRegistry is NULL in OnSnapshotReceived");
            return;
        }
        
        RefreshDisplay();
    }

    public void OnClueObtained(int playerId, string clueId)
    {
        if (!initialized || playerId != localPlayerId)
        {
            return;
        }

        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (!initialized || clueRegistry == null || mainClueImage == null)
        {
            Debug.LogWarning($"[EscapistCluesDisplay] Cannot update display: initialized={initialized}, registry={clueRegistry != null}, image={mainClueImage != null}");
            return;
        }

        int clueCount = clueRegistry.GetGlobalCollectedCount();

        Sprite newSprite = GetSpriteForClueCount(clueCount);
        if (newSprite != null)
        {
            mainClueImage.sprite = newSprite;
        }
        else
        {
            Debug.LogWarning($"[EscapistCluesDisplay] Null sprite for count={clueCount}");
        }
    }

    private Sprite GetSpriteForClueCount(int count)
    {
        return count switch
        {
            0 => clue0Sprite,
            1 => clue1Sprite,
            2 => clue2Sprite,
            >= 3 => clue3Sprite,
            _ => null
        };
    }

    public int GetGlobalCollectedCount()
    {
        return clueRegistry.GetGlobalCollectedCount();
    }
}
