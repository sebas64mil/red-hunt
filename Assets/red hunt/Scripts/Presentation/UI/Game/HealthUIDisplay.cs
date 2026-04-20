using UnityEngine;
using UnityEngine.UI;

public class HealthUIDisplay : MonoBehaviour
{
    [SerializeField] private Image healthImage;
    [SerializeField] private Sprite health3Sprite;
    [SerializeField] private Sprite health2Sprite;
    [SerializeField] private Sprite health1Sprite;
    [SerializeField] private Sprite health0Sprite;

    [Header("Death Camera")]
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
            Debug.LogError("[HealthUIDisplay] Image component not assigned");
            return;
        }

        if (health3Sprite == null || health2Sprite == null || health1Sprite == null || health0Sprite == null)
        {
            Debug.LogWarning("[HealthUIDisplay] One of the health sprites is not assigned");
        }

        if (deathCameraGameObject == null)
        {
            Debug.LogWarning("[HealthUIDisplay] Death Camera GameObject not assigned");
        }

    }

    public void Init(EscapistHealth health, int id)
    {
        if (health == null)
        {
            Debug.LogError("[HealthUIDisplay] EscapistHealth is NULL");
            return;
        }

        escapistHealth = health;
        playerId = id;
        isInitialized = true;

        escapistHealth.OnHealthChanged += HandleHealthChanged;
        escapistHealth.OnPlayerDied += HandlePlayerDied;

        UpdateHealthDisplay();

    }

    public void DisableForKiller()
    {
        if (healthImage != null)
        {
            healthImage.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[HealthUIDisplay] healthImage is NULL in DisableForKiller()");
        }
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        UpdateHealthDisplay();
        
        if (currentHealth == 0)
        {
            ActivateDeathCamera();
        }
    }

    private void HandlePlayerDied(int deadPlayerId)
    {
        if (deadPlayerId == playerId)
        {
            Debug.Log($"[HealthUIDisplay] Local player {playerId} has died");
        }
    }

    private void ActivateDeathCamera()
    {

        DisablePlayerCameras();

        if (deathCameraGameObject != null)
        {
            deathCameraGameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[HealthUIDisplay] Death Camera GameObject is not assigned");
        }
    }

    private void DisablePlayerCameras()
    {
        if (escapistHealth == null || escapistHealth.gameObject == null)
        {
            Debug.LogWarning("[HealthUIDisplay] Cannot disable cameras - gameObject is NULL");
            return;
        }

        GameObject playerGO = escapistHealth.gameObject;

        var cameraHolder = playerGO.transform.Find("CameraHolder");
        if (cameraHolder != null)
        {
            cameraHolder.gameObject.SetActive(false);
            return;
        }

        var transforms = playerGO.GetComponentsInChildren<Transform>();
        foreach (var t in transforms)
        {
            if (t.name.Contains("Camera") || t.name.Contains("Cinemachine"))
            {
                t.gameObject.SetActive(false);
                return;
            }
        }

        Debug.LogWarning("[HealthUIDisplay] CameraHolder or cameras not found");
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
        }
        else
        {
            Debug.LogWarning($"[HealthUIDisplay] Null sprite for health={currentHealth}");
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
        }
    }
}