using UnityEngine;
using UnityEngine.SceneManagement;

public class CursorMonitor : MonoBehaviour
{
    private void Awake()
    {
        // Hacer que este objeto persista entre escenas
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[CursorMonitor] Escena cargada: {scene.name}");
        LogCursorStatus("OnSceneLoaded");
    }

    private void Update()
    {
        // Verificar cambios en el cursor cada frame (solo en desarrollo)
        if (Application.isEditor && Time.frameCount % 60 == 0) // Cada ~1 segundo a 60 FPS
        {
            LogCursorStatus("PeriodicCheck");
        }
    }

    private void LogCursorStatus(string context)
    {
        string status = $"[CursorMonitor:{context}] IsHost={GameManager.IsHost}, Cursor.visible={Cursor.visible}, Cursor.lockState={Cursor.lockState}, Scene={SceneManager.GetActiveScene().name}";
        Debug.Log(status);
    }

    // Método público para forzar log
    public void LogStatus()
    {
        LogCursorStatus("Manual");
    }
}