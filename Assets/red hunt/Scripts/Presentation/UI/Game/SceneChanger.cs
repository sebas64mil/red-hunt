using UnityEngine;

public class SceneChanger : MonoBehaviour
{
    [Tooltip("Nombre de la escena a cargar. Se puede escribir aquí y reutilizar desde el Inspector.")]
    [SerializeField] private string sceneName;

    public void SetSceneName(string name)
    {
        sceneName = name;
    }

    // Llamar desde Button.onClick en el Inspector (sin parámetros).
    public void StartGame()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("SceneChanger: sceneName vacío.");
            return;
        }

        // Desacoplamiento: solicitar StartGame al Orquestador (raise del evento internamente).
        if (ModularLobbyBootstrap.Instance != null)
        {
            ModularLobbyBootstrap.Instance.RequestStartGame(sceneName);
            return;
        }

        // Fallback: cambiar escena localmente
        GameManager.ChangeScene(sceneName);
    }

    // Método utilitario público para invocar cambio de escena sin tocar red (útil en clientes en tests)
    public void ChangeSceneLocalOnly()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("SceneChanger: sceneName vacío.");
            return;
        }

        GameManager.ChangeScene(sceneName);
    }
}