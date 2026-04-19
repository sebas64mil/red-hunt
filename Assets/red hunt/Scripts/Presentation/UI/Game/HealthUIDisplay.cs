using UnityEngine;
using UnityEngine.UI;

public class HealthUIDisplay : MonoBehaviour
{
    [SerializeField] private Image healthImage;
    [SerializeField] private Sprite health3Sprite;
    [SerializeField] private Sprite health2Sprite;
    [SerializeField] private Sprite health1Sprite;
    [SerializeField] private Sprite health0Sprite;

    [Header("Cámara de Muerte")]
    [SerializeField] private GameObject deathCameraGameObject;

    private EscapistHealth escapistHealth;
    private int playerId = -1;
    private bool isInitialized = false;

    private void Awake()
    {
        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (healthImage == null)
        {
            Debug.LogError("[HealthUIDisplay] ❌ Image component no asignado");
            return;
        }

        if (health3Sprite == null || health2Sprite == null || health1Sprite == null || health0Sprite == null)
        {
            Debug.LogWarning("[HealthUIDisplay] ⚠️ Alguno de los sprites no está asignado");
        }

        if (deathCameraGameObject == null)
        {
            Debug.LogWarning("[HealthUIDisplay] ⚠️ Death Camera GameObject no asignado");
        }

        Debug.Log("[HealthUIDisplay] ✅ Componentes validados");
    }

    public void Init(EscapistHealth health, int id)
    {
        if (health == null)
        {
            Debug.LogError("[HealthUIDisplay] ❌ EscapistHealth es NULL");
            return;
        }

        escapistHealth = health;
        playerId = id;
        isInitialized = true;

        // Suscribirse a eventos de cambio de salud
        escapistHealth.OnHealthChanged += HandleHealthChanged;
        escapistHealth.OnPlayerDied += HandlePlayerDied;

        // Mostrar salud actual
        UpdateHealthDisplay();

        Debug.Log($"[HealthUIDisplay] ✅ Inicializado para Escapist {playerId}");
    }

    public void DisableForKiller()
    {
        if (healthImage != null)
        {
            healthImage.gameObject.SetActive(false);
            Debug.Log("[HealthUIDisplay] 🔪 Health Image desactivada (Killer no necesita salud)");
        }
        else
        {
            Debug.LogWarning("[HealthUIDisplay] ⚠️ healthImage es NULL en DisableForKiller()");
        }
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        UpdateHealthDisplay();
        
        // ⭐ NUEVO: Si la salud llega a 0, activar cámara de muerte
        if (currentHealth == 0)
        {
            ActivateDeathCamera();
        }
    }

    private void HandlePlayerDied(int deadPlayerId)
    {
        // Solo log, la lógica ya está en HandleHealthChanged
        if (deadPlayerId == playerId)
        {
            Debug.Log($"[HealthUIDisplay] 💀 Jugador local {playerId} ha muerto");
        }
    }

    private void ActivateDeathCamera()
    {
        Debug.Log("[HealthUIDisplay] 📷 Salud llegó a 0 - activando cámara de muerte");

        // Desactivar cámaras del player
        DisablePlayerCameras();

        // Activar cámara de muerte
        if (deathCameraGameObject != null)
        {
            deathCameraGameObject.SetActive(true);
            Debug.Log("[HealthUIDisplay] ✅ Death Camera activada");
        }
        else
        {
            Debug.LogWarning("[HealthUIDisplay] ⚠️ Death Camera GameObject no está asignado");
        }
    }

    private void DisablePlayerCameras()
    {
        if (escapistHealth == null || escapistHealth.gameObject == null)
        {
            Debug.LogWarning("[HealthUIDisplay] ⚠️ No se puede desactivar cámaras - gameObject es NULL");
            return;
        }

        GameObject playerGO = escapistHealth.gameObject;

        // Buscar CameraHolder por nombre
        var cameraHolder = playerGO.transform.Find("CameraHolder");
        if (cameraHolder != null)
        {
            cameraHolder.gameObject.SetActive(false);
            Debug.Log("[HealthUIDisplay] 📷 CameraHolder desactivado");
            return;
        }

        // Fallback: Buscar cualquier transform que contenga "Camera" en el nombre
        var transforms = playerGO.GetComponentsInChildren<Transform>();
        foreach (var t in transforms)
        {
            if (t.name.Contains("Camera") || t.name.Contains("Cinemachine"))
            {
                t.gameObject.SetActive(false);
                Debug.Log($"[HealthUIDisplay] 📷 Desactivada cámara: {t.name}");
                return;
            }
        }

        Debug.LogWarning("[HealthUIDisplay] ⚠️ No se encontró CameraHolder ni cámaras");
    }

    private void UpdateHealthDisplay()
    {
        if (!isInitialized || escapistHealth == null || healthImage == null)
        {
            return;
        }

        int currentHealth = escapistHealth.CurrentHealth;

        Sprite newSprite = GetSpriteForHealth(currentHealth);

        if (newSprite != null)
        {
            healthImage.sprite = newSprite;
            Debug.Log($"[HealthUIDisplay] 🏥 Sprite actualizado para Escapist {playerId}: salud={currentHealth}");
        }
        else
        {
            Debug.LogWarning($"[HealthUIDisplay] ⚠️ Sprite NULL para salud={currentHealth}");
        }
    }

    private Sprite GetSpriteForHealth(int health)
    {
        return health switch
        {
            3 => health3Sprite,
            2 => health2Sprite,
            1 => health1Sprite,
            0 => health0Sprite,
            _ => health0Sprite
        };
    }

    private void OnDestroy()
    {
        if (escapistHealth != null)
        {
            escapistHealth.OnHealthChanged -= HandleHealthChanged;
            escapistHealth.OnPlayerDied -= HandlePlayerDied;
            Debug.Log($"[HealthUIDisplay] 🗑️ Desuscrito de eventos para player {playerId}");
        }
    }
}