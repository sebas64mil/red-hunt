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

        // ⭐ CAMBIO: NO llamar a RefreshDisplay() aquí
        // Esperar a que llegue el snapshot del servidor
        Debug.Log($"[EscapistCluesDisplay] ✅ Inicializado para Escapist {playerId} (esperando snapshot...)");
    }

    public void HideForKiller()
    {
        if (mainClueImage != null)
        {
            mainClueImage.gameObject.SetActive(false);
            Debug.Log("[EscapistCluesDisplay] 🔪 Clues Display desactivado (Killer)");
        }
    }

    /// <summary>
    /// Método llamado cuando llega el snapshot de pistas de la red
    /// </summary>
    public void OnSnapshotReceived(IReadOnlyDictionary<int, IReadOnlyCollection<string>> cluesByEscapist)
    {
        if (!initialized || localPlayerId <= 0)
        {
            return;
        }

        Debug.Log($"[EscapistCluesDisplay] 📸 Snapshot recibido - actualizando display para player {localPlayerId}");
        
        // ⭐ CRÍTICO: Sincronizar el registro ANTES de refrescar
        if (clueRegistry != null)
        {
            clueRegistry.SyncFromSnapshot(cluesByEscapist);
            Debug.Log("[EscapistCluesDisplay] ✅ Registro sincronizado desde snapshot");
        }
        else
        {
            Debug.LogError("[EscapistCluesDisplay] ❌ ClueRegistry es NULL en OnSnapshotReceived");
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

        Debug.Log($"[EscapistCluesDisplay] 🔑 OnClueObtained: {clueId}");
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (!initialized || clueRegistry == null || mainClueImage == null)
        {
            Debug.LogWarning($"[EscapistCluesDisplay] ⚠️ No se puede actualizar: initialized={initialized}, registry={clueRegistry != null}, image={mainClueImage != null}");
            return;
        }

        // ⭐ GLOBAL: no dependemos de GetSnapshot()/targetEscapistIds
        int clueCount = clueRegistry.GetGlobalCollectedCount();

        Sprite newSprite = GetSpriteForClueCount(clueCount);
        if (newSprite != null)
        {
            mainClueImage.sprite = newSprite;
            Debug.Log($"[EscapistCluesDisplay] ✅ Sprite actualizado (GLOBAL): {clueCount}/3 pistas");
        }
        else
        {
            Debug.LogWarning($"[EscapistCluesDisplay] ⚠️ Sprite NULL para count={clueCount}");
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
