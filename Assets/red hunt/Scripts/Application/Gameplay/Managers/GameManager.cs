using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameManager
{
    public static bool IsPaused { get; private set; } = false;
    public static bool IsHost { get; set; } = false;
    public static AdminNetworkService AdminService { get; set; }

    public static void SetCursorVisible(bool state)
    {
        Cursor.visible = state;
        Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public static void ChangeScene(string sceneName)
    {
        Time.timeScale = 1f;
        IsPaused = false;
        SceneManager.LoadScene(sceneName);
    }

    public static void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }

    public static void SetPause(bool pause)
    {
        IsPaused = pause;
        Time.timeScale = pause ? 0f : 1f;
    }

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
                Debug.LogWarning("[GameManager] Could not send global pause to server");
            }
        }
        else if (IsHost)
        {
            Debug.LogWarning("[GameManager] AdminService not initialized; applying pause locally");
            SetPause(newPause);
        }
        else
        {
            Debug.LogWarning("[GameManager] Only host can toggle global pause");
        }
    }

    public static void RestartScene()
    {
        Time.timeScale = 1f;
        IsPaused = false;

        string currentScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentScene);
    }
}