using UnityEngine;
using UnityEngine.SceneManagement;

public class CursorMonitor : MonoBehaviour
{
    private void Awake()
    {
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
        LogCursorStatus("OnSceneLoaded");
    }

    private void Update()
    {
        if (Application.isEditor && Time.frameCount % 60 == 0)
        {
            LogCursorStatus("PeriodicCheck");
        }
    }

    private void LogCursorStatus(string context)
    {
        string status = $"[CursorMonitor:{context}] IsHost={GameManager.IsHost}, Cursor.visible={Cursor.visible}, Cursor.lockState={Cursor.lockState}, Scene={SceneManager.GetActiveScene().name}";
    }

    public void LogStatus()
    {
        LogCursorStatus("Manual");
    }
}