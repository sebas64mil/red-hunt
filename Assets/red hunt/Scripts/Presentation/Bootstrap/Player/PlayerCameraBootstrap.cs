using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerCameraBootstrap : MonoBehaviour
{
    [Header("Escenas (configurar en el Inspector)")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private string gameSceneName = "Game";

    // Id del jugador local (setear cuando el cliente conozca su id)
    private int localPlayerId = -1;

    // Referencias conocidas a PlayerView (se registran al spawnear)
    private readonly List<PlayerView> registeredViews = new();

    private void Awake()
    {
        // Persistente: asume que el GameObject ya está marcado DontDestroyOnLoad en ModularLobbyBootstrap
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneLogic(scene.name);
    }

    // Public API: llamada por el componente de red / spawn cuando se conoce el id local
    public void SetLocalPlayerId(int id)
    {
        localPlayerId = id;
        // Reaplicar lógica cuando se conozca el id
        ApplySceneLogic(SceneManager.GetActiveScene().name);
    }

    // Public API: registrar PlayerView al instanciar prefabs (evita FindObjectsOfType)
    public void RegisterPlayerView(PlayerView view)
    {
        if (view == null) return;
        if (!registeredViews.Contains(view))
            registeredViews.Add(view);

        // Aplicar estado actual inmediatamente (evita race conditions)
        ApplyStateToView(view, SceneManager.GetActiveScene().name);
    }

    // Public API: para limpieza cuando se destruye un player
    public void UnregisterPlayerView(PlayerView view)
    {
        if (view == null) return;
        registeredViews.Remove(view);
    }

    // Reaplica la lógica de cámaras según la escena actual (sin usar FindObjectsOfType)
    private void ApplySceneLogic(string sceneName)
    {
        // Iterar sobre una copia para permitir modificaciones dentro del bucle
        foreach (var v in registeredViews.ToArray())
        {
            if (v == null)
            {
                registeredViews.Remove(v);
                continue;
            }

            ApplyStateToView(v, sceneName);
        }
    }

    // Decide y aplica el estado a una vista concreta
    private void ApplyStateToView(PlayerView view, string sceneName)
    {
        if (view == null) return;

        // En lobby: desactivar cámaras de player
        if (!string.IsNullOrWhiteSpace(lobbySceneName) && sceneName == lobbySceneName)
        {
            view.SetLocal(false);
            view.DeactivateLocalCamera();
            return;
        }

        // En game: marcar local sólo al id conocido y activar/desactivar la vCam correspondiente
        if (!string.IsNullOrWhiteSpace(gameSceneName) && sceneName == gameSceneName)
        {
            bool isLocal = (localPlayerId != -1 && view.PlayerId == localPlayerId);
            view.SetLocal(isLocal);

            if (isLocal)
                view.ActivateLocalCamera();
            else
                view.DeactivateLocalCamera();

            return;
        }

        // Otras escenas: desactivar
        view.SetLocal(false);
        view.DeactivateLocalCamera();
    }
}