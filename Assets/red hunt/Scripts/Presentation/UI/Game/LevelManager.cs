using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class LevelManager : MonoBehaviour
{       
    public static LevelManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject hostPanel;
    [SerializeField] private TextMeshProUGUI mouseStatusText;

    [Header("Input System (opcional)")]
    [SerializeField] private InputActionReference pauseActionReference;
    [SerializeField] private InputActionReference hostActionReference;

    private InputAction pauseAction;
    private Action<InputAction.CallbackContext> pauseCallback;

    private InputAction hostAction;
    private Action<InputAction.CallbackContext> hostCallback;

    private bool isLocallyPaused = false;
    private bool isMouseEnabled = false;

    public static bool IsLocallyPaused => Instance != null && Instance.isLocallyPaused;
    public static bool IsMouseEnabled => Instance != null && Instance.isMouseEnabled;

    public static event Action<bool> OnLocalPauseChanged;
    public static event Action<bool> OnMouseStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LevelManager] Ya existe una instancia, destruyendo la nueva.");
            Destroy(this);
            return;
        }

        Instance = this;

        if (pausePanel != null)
            pausePanel.SetActive(isLocallyPaused);

        if (mouseStatusText != null)
            mouseStatusText.gameObject.SetActive(true);

        UpdateMouseStatusDisplay();
    }

    private void OnEnable()
    {
        if (pauseActionReference != null)
            RegisterPauseActionInternal(pauseActionReference.action);

        if (hostActionReference != null)
            RegisterHostActionInternal(hostActionReference.action);
    }

    private void OnDisable()
    {
        UnregisterPauseActionInternal();
        UnregisterHostActionInternal();
    }

    private void Update()
    {
        // Sincronizar el panel de pausa con la pausa global
        if (GameManager.IsPaused != isLocallyPaused)
        {
            SetLocalPauseInstance(GameManager.IsPaused);
        }

        // Actualizar visibilidad del texto del mouse según host y cursor visible
        UpdateMouseStatusTextVisibility();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }


    public void RegisterPausePanelInstance(GameObject panel)
    {
        pausePanel = panel;
        if (pausePanel != null)
            pausePanel.SetActive(isLocallyPaused);
    }

    public void UnregisterPausePanelInstance()
    {
        pausePanel = null;
    }

    public void RegisterHostPanelInstance(GameObject panel)
    {
        hostPanel = panel;
        if (hostPanel != null)
            hostPanel.SetActive(!isLocallyPaused);
    }

    public void UnregisterHostPanelInstance()
    {
        hostPanel = null;
    }

    public void RegisterMouseStatusTextInstance(TextMeshProUGUI text)
    {
        mouseStatusText = text;
        UpdateMouseStatusDisplay();
    }

    public void UnregisterMouseStatusTextInstance()
    {
        mouseStatusText = null;
    }

    public void RegisterPauseActionInstance(InputAction action)
    {
        RegisterPauseActionInternal(action);
    }

    public void UnregisterPauseActionInstance()
    {
        UnregisterPauseActionInternal();
    }

    public void RegisterHostActionInstance(InputAction action)
    {
        RegisterHostActionInternal(action);
    }

    public void UnregisterHostActionInstance()
    {
        UnregisterHostActionInternal();
    }

    public void ToggleLocalPauseInstance()
    {
        if (GameManager.IsHost)
        {
            GameManager.TogglePause();
        }
        else
        {
            Debug.LogWarning("Solo el host puede continuar el juego");
        }
    }

    public void ToggleMouseInstance()
    {
        if (GameManager.IsHost)
        {
            SetMouseEnabledInstance(!isMouseEnabled);
        }
        else
        {
            Debug.LogWarning("[LevelManager] Solo el host puede controlar el mouse");
        }
    }

    public void SetLocalPauseInstance(bool pause)
    {
        if (pause == isLocallyPaused) return;

        if (pausePanel != null)
            pausePanel.SetActive(pause);

        if (hostPanel != null)
            hostPanel.SetActive(!pause);

        GameManager.SetCursorVisible(pause);

        isLocallyPaused = pause;

        OnLocalPauseChanged?.Invoke(isLocallyPaused);

    }

    public void SetMouseEnabledInstance(bool enabled)
    {
        if (enabled == isMouseEnabled) return;

        isMouseEnabled = enabled;
        GameManager.SetCursorVisible(enabled);
        UpdateMouseStatusDisplay();
        OnMouseStateChanged?.Invoke(isMouseEnabled);

        Debug.Log($"[LevelManager] Mouse habilitado: {isMouseEnabled}");
    }

    public void ChangeSceneInstance(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[LevelManager] ChangeScene: sceneName vacío");
            return;
        }

        GameManager.ChangeScene(sceneName);
    }

    private void UpdateMouseStatusDisplay()
    {
        if (mouseStatusText != null)
        {
            mouseStatusText.text = isMouseEnabled ? "Mouse: ON" : "Mouse: OFF";
            mouseStatusText.color = isMouseEnabled ? Color.green : Color.red;
        }
    }

    private void UpdateMouseStatusTextVisibility()
    {
        if (mouseStatusText == null) return;

        // Solo visible si es host, NO está pausado Y el mouse está habilitado
        bool shouldBeVisible = GameManager.IsHost && !isLocallyPaused && isMouseEnabled;
        mouseStatusText.gameObject.SetActive(shouldBeVisible);
    }

    private void RegisterPauseActionInternal(InputAction action)
    {
        if (action == null)
        {
            Debug.LogError("[LevelManager] ❌ pauseActionReference es NULL. Asigna una acción en el Inspector.");
            return;
        }

        UnregisterPauseActionInternal();

        pauseAction = action;
        pauseCallback = ctx =>
        {
            var deviceName = ctx.control?.device?.displayName ?? "UNKNOWN";
            var controlPath = ctx.control?.path ?? "UNKNOWN";
            ToggleLocalPauseInstance();
        };

        try
        {
            pauseAction.Enable();
            pauseAction.performed += pauseCallback;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelManager] ❌ Error registrando Pause action: {e.Message}");
        }
    }

    private void UnregisterPauseActionInternal()
    {
        if (pauseAction != null && pauseCallback != null)
        {
            try
            {
                pauseAction.performed -= pauseCallback;
                pauseAction.Disable();
            }
            catch { }
        }

        pauseAction = null;
        pauseCallback = null;
    }

    private void RegisterHostActionInternal(InputAction action)
    {
        if (action == null)
        {
            Debug.LogError("[LevelManager] ❌ hostActionReference es NULL. Asigna una acción en el Inspector.");
            return;
        }

        UnregisterHostActionInternal();

        hostAction = action;
        hostCallback = ctx =>
        {
            ToggleMouseInstance();
        };

        try
        {
            hostAction.Enable();
            hostAction.performed += hostCallback;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelManager] ❌ Error registrando Host action: {e.Message}");
        }
    }

    private void UnregisterHostActionInternal()
    {
        if (hostAction != null && hostCallback != null)
        {
            try
            {
                hostAction.performed -= hostCallback;
                hostAction.Disable();
            }
            catch { }
        }

        hostAction = null;
        hostCallback = null;
    }

    public static void RegisterPausePanel(GameObject panel)
    {
        if (Instance != null)
            Instance.RegisterPausePanelInstance(panel);
        else
            Debug.LogWarning("[LevelManager] Ninguna instancia en escena al registrar panel. Añade LevelManager a la escena.");
    }

    public static void UnregisterPausePanel()
    {
        if (Instance != null)
            Instance.UnregisterPausePanelInstance();
    }

    public static void RegisterHostPanel(GameObject panel)
    {
        if (Instance != null)
            Instance.RegisterHostPanelInstance(panel);
        else
            Debug.LogWarning("[LevelManager] Ninguna instancia en escena al registrar panel. Añade LevelManager a la escena.");
    }

    public static void UnregisterHostPanel()
    {
        if (Instance != null)
            Instance.UnregisterHostPanelInstance();
    }

    public static void RegisterMouseStatusText(TextMeshProUGUI text)
    {
        if (Instance != null)
            Instance.RegisterMouseStatusTextInstance(text);
        else
            Debug.LogWarning("[LevelManager] Ninguna instancia en escena al registrar texto. Añade LevelManager a la escena.");
    }

    public static void UnregisterMouseStatusText()
    {
        if (Instance != null)
            Instance.UnregisterMouseStatusTextInstance();
    }

    public static void RegisterPauseAction(InputAction action)
    {
        if (Instance != null)
            Instance.RegisterPauseActionInstance(action);
        else
            Debug.LogWarning("[LevelManager] Ninguna instancia en escena al registrar action. Añade LevelManager a la escena.");
    }

    public static void UnregisterPauseAction()
    {
        if (Instance != null)
            Instance.UnregisterPauseActionInstance();
    }

    public static void RegisterHostAction(InputAction action)
    {
        if (Instance != null)
            Instance.RegisterHostActionInstance(action);
        else
            Debug.LogWarning("[LevelManager] Ninguna instancia en escena al registrar host action. Añade LevelManager a la escena.");
    }

    public static void UnregisterHostAction()
    {
        if (Instance != null)
            Instance.UnregisterHostActionInstance();
    }

    public static void ToggleLocalPause()
    {
        if (Instance != null)
            Instance.ToggleLocalPauseInstance();
        else
            Debug.LogWarning("[LevelManager] Ninguna instancia en escena al togglear pausa. Añade LevelManager a la escena.");
    }

    public static void ToggleMouse()
    {
        if (Instance != null)
            Instance.ToggleMouseInstance();
        else
            Debug.LogWarning("[LevelManager] Ninguna instancia en escena al togglear mouse. Añade LevelManager a la escena.");
    }

    public static void SetLocalPause(bool pause)
    {
        if (Instance != null)
            Instance.SetLocalPauseInstance(pause);
        else
            Debug.LogWarning("[LevelManager] Ninguna instancia en escena al setear pausa. Añade LevelManager a la escena.");
    }

    public static void SetMouseEnabled(bool enabled)
    {
        if (Instance != null)
            Instance.SetMouseEnabledInstance(enabled);
        else
            Debug.LogWarning("[LevelManager] Ninguna instancia en escena al setear mouse. Añade LevelManager a la escena.");
    }

    public static void ChangeScene(string sceneName)
    {
        if (Instance != null)
            Instance.ChangeSceneInstance(sceneName);
        else
            GameManager.ChangeScene(sceneName); 
    }
}