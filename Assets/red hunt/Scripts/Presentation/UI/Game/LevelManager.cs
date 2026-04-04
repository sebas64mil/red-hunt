using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class LevelManager : MonoBehaviour
{       
    public static LevelManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject pausePanel;

    [Header("Input System (opcional)")]
    [SerializeField] private InputActionReference pauseActionReference;

    private InputAction pauseAction;
    private Action<InputAction.CallbackContext> pauseCallback;

    private bool isLocallyPaused = false;

    public static bool IsLocallyPaused => Instance != null && Instance.isLocallyPaused;

    public static event Action<bool> OnLocalPauseChanged;

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
    }

    private void OnEnable()
    {
        if (pauseActionReference != null)
            RegisterPauseActionInternal(pauseActionReference.action);
    }

    private void OnDisable()
    {
        UnregisterPauseActionInternal();
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

    public void RegisterPauseActionInstance(InputAction action)
    {
        RegisterPauseActionInternal(action);
    }

    public void UnregisterPauseActionInstance()
    {
        UnregisterPauseActionInternal();
    }

    public void ToggleLocalPauseInstance()
    {
        SetLocalPauseInstance(!isLocallyPaused);
    }

    public void SetLocalPauseInstance(bool pause)
    {
        if (pause == isLocallyPaused) return;

        if (pausePanel != null)
            pausePanel.SetActive(pause);

        GameManager.SetCursorVisible(pause);

        isLocallyPaused = pause;

        OnLocalPauseChanged?.Invoke(isLocallyPaused);

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

    public static void ToggleLocalPause()
    {
        if (Instance != null)
            Instance.ToggleLocalPauseInstance();
        else
            Debug.LogWarning("[LevelManager] Ninguna instancia en escena al togglear pausa. Añade LevelManager a la escena.");
    }

    public static void SetLocalPause(bool pause)
    {
        if (Instance != null)
            Instance.SetLocalPauseInstance(pause);
        else
            Debug.LogWarning("[LevelManager] Ninguna instancia en escena al setear pausa. Añade LevelManager a la escena.");
    }

    public static void ChangeScene(string sceneName)
    {
        if (Instance != null)
            Instance.ChangeSceneInstance(sceneName);
        else
            GameManager.ChangeScene(sceneName); 
    }
}