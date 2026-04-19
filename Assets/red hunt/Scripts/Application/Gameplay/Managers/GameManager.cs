using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameManager
{
    public static bool IsPaused { get; private set; } = false;
    public static bool IsHost { get; set; } = false;
    public static AdminNetworkService AdminService { get; set; }

    // ----------------- Enable or disable cursor ------------------
    public static void SetCursorVisible(bool state)
    {
        Cursor.visible = state;
        Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
    }

    // ----------------- Change scene ------------------
    public static void ChangeScene(string sceneName)
    {
        Time.timeScale = 1f;
        IsPaused = false;
        SceneManager.LoadScene(sceneName);
    }

    // ----------------- Quit game ------------------
    public static void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }

    // ----------------- Pause / Resume ------------------
    public static void SetPause(bool pause)
    {
        IsPaused = pause;
        Time.timeScale = pause ? 0f : 1f;
    }

    // ----------------- Toggle Pause ------------------
    public static async void TogglePause()
    {
        bool newPause = !IsPaused;

        if (IsHost && AdminService != null)
        {
            bool success = await AdminService.SetGlobalPause(newPause);
            if (success)
            {
                SetPause(newPause);
            }
            else
            {
                Debug.LogWarning("[GameManager] No se pudo enviar la pausa global al servidor");
            }
        }
        else if (IsHost)
        {
            Debug.LogWarning("[GameManager] AdminService no inicializado; aplicando pausa localmente");
            SetPause(newPause);
        }
        else
        {
            Debug.LogWarning("[GameManager] Solo el host puede alternar la pausa global");
        }
    }

    // ----------------- Restart current scene ------------------
    public static void RestartScene()
    {
        Time.timeScale = 1f;
        IsPaused = false;

        string currentScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentScene);
    }
}