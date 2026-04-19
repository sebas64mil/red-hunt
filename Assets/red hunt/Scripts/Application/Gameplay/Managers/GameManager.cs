using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameManager
{
    public static bool IsPaused { get; private set; } = false;
    public static bool IsHost { get; set; } = false;

    // ----------------- Enable or disable cursor ------------------
    public static void SetCursorVisible(bool state)
    {
        bool actualState = IsHost ? true : state;
        Cursor.visible = actualState;
        Cursor.lockState = actualState ? CursorLockMode.None : CursorLockMode.Locked;
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
    public static void TogglePause()
    {
        SetPause(!IsPaused);
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