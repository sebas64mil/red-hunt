using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyCameraManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "Lobby";

    [Header("Lobby Cameras")]
    [SerializeField] private GameObject killerLobbyCameraObject;
    [SerializeField] private GameObject escapistLobbyCameraObject;

    private NetworkBootstrap networkBootstrap;
    private ApplicationBootstrap applicationBootstrap;

    private int localPlayerId = -1;

    private void Awake()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        Unsubscribe();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != lobbySceneName)
        {
            SetAllCamerasActive(false);
            return;
        }

        ResolveBootstraps();
        Subscribe();

        Refresh();
    }

    private void ResolveBootstraps()
    {
        var boot = ModularLobbyBootstrap.Instance;
        if (boot == null) return;

        networkBootstrap = boot.GetComponent<NetworkBootstrap>();
        applicationBootstrap = boot.GetComponent<ApplicationBootstrap>();
    }

    private void Subscribe()
    {
        Unsubscribe();

        if (networkBootstrap?.Services?.ClientState != null)
        {
            networkBootstrap.Services.ClientState.OnPlayerIdAssigned -= HandlePlayerIdAssigned;
            networkBootstrap.Services.ClientState.OnPlayerIdAssigned += HandlePlayerIdAssigned;
        }

        if (applicationBootstrap != null)
        {
            applicationBootstrap.OnPlayerJoined -= HandleRosterChanged;
            applicationBootstrap.OnPlayerJoined += HandleRosterChanged;

            applicationBootstrap.OnPlayerLeft -= HandleRosterChanged;
            applicationBootstrap.OnPlayerLeft += HandleRosterChanged;
        }
    }

    private void Unsubscribe()
    {
        if (networkBootstrap?.Services?.ClientState != null)
        {
            networkBootstrap.Services.ClientState.OnPlayerIdAssigned -= HandlePlayerIdAssigned;
        }

        if (applicationBootstrap != null)
        {
            applicationBootstrap.OnPlayerJoined -= HandleRosterChanged;
            applicationBootstrap.OnPlayerLeft -= HandleRosterChanged;
        }
    }

    private void HandlePlayerIdAssigned(int id)
    {
        localPlayerId = id;
        Refresh();
    }

    private void HandleRosterChanged(PlayerSession _)
    {
        Refresh();
    }

    private void HandleRosterChanged(int _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (SceneManager.GetActiveScene().name != lobbySceneName)
        {
            return;
        }

        if (localPlayerId <= 0)
        {
            localPlayerId = networkBootstrap?.Services?.ClientState?.PlayerId ?? -1;
        }

        var lobbyManager = applicationBootstrap?.Services?.LobbyManager;
        if (lobbyManager == null || localPlayerId <= 0)
        {
            // Si aún no hay datos, deja todo apagado para evitar doble cámara
            SetAllCamerasActive(false);
            return;
        }

        var me = lobbyManager.GetAllPlayers()?.FirstOrDefault(p => p.Id == localPlayerId);
        if (me == null)
        {
            SetAllCamerasActive(false);
            return;
        }

        bool isKiller = string.Equals(me.PlayerType, PlayerType.Killer.ToString(), StringComparison.OrdinalIgnoreCase);
        SetRoleCamera(isKiller);
    }

    private void SetRoleCamera(bool isKiller)
    {
        if (killerLobbyCameraObject != null)
        {
            killerLobbyCameraObject.SetActive(isKiller);
        }

        if (escapistLobbyCameraObject != null)
        {
            escapistLobbyCameraObject.SetActive(!isKiller);
        }

        Debug.Log($"[LobbyCameraManager] Cámara lobby aplicada. isKiller={isKiller}, localPlayerId={localPlayerId}");
    }

    private void SetAllCamerasActive(bool active)
    {
        if (killerLobbyCameraObject != null) killerLobbyCameraObject.SetActive(active);
        if (escapistLobbyCameraObject != null) escapistLobbyCameraObject.SetActive(active);
    }
}