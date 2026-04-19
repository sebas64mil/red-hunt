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
        if (IsHost && AdminService != null)
        {
            bool newPause = !IsPaused;
            await AdminService.SetGlobalPause(newPause);
            SetPause(newPause); // Aplicar localmente también
        }
        else if (!IsHost)
        {
            Debug.LogWarning("Solo el host puede pausar el juego");
        }
        else
        {
            Debug.LogWarning("AdminService no inicializado");
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