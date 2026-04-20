using UnityEngine;

public class SceneChanger : MonoBehaviour
{
    [SerializeField] private string sceneName;

    public void SetSceneName(string name)
    {
        sceneName = name;
    }

    public void StartGame()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneChanger] sceneName is empty");
            return;
        }

        if (ModularLobbyBootstrap.Instance != null)
        {
            ModularLobbyBootstrap.Instance.RequestStartGame(sceneName);
            return;
        }

        GameManager.ChangeScene(sceneName);
    }

    public void ChangeSceneLocalOnly()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneChanger] sceneName is empty");
            return;
        }

        GameManager.ChangeScene(sceneName);
    }
}