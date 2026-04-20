using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerCameraBootstrap : MonoBehaviour
{
    [Header("Scenes (configure in Inspector)")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private string gameSceneName = "Game";

    private int localPlayerId = -1;

    private readonly List<PlayerView> registeredViews = new();

    private void Awake()
    {
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

    public void SetLocalPlayerId(int id)
    {
        localPlayerId = id;
        ApplySceneLogic(SceneManager.GetActiveScene().name);
    }

    public void RegisterPlayerView(PlayerView view)
    {
        if (view == null) return;
        if (!registeredViews.Contains(view))
            registeredViews.Add(view);

        ApplyStateToView(view, SceneManager.GetActiveScene().name);
    }

    public void UnregisterPlayerView(PlayerView view)
    {
        if (view == null) return;
        registeredViews.Remove(view);
    }

    private void ApplySceneLogic(string sceneName)
    {
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

    private void ApplyStateToView(PlayerView view, string sceneName)
    {
        if (view == null) return;

        if (!string.IsNullOrWhiteSpace(lobbySceneName) && sceneName == lobbySceneName)
        {
            view.SetLocal(false);
            view.DeactivateLocalCamera();
            return;
        }

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

        view.SetLocal(false);
        view.DeactivateLocalCamera();
    }
}