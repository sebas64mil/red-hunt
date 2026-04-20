using System;
using UnityEngine;

public class ApplicationBootstrap : MonoBehaviour
{
    public ApplicationServices Services { get; private set; }

    public event Action<PlayerSession> OnPlayerJoined;
    public event Action<int> OnPlayerLeft;

    private bool installed = false;

    public void Init()
    {
        if (installed) return;

        try
        {
            var installer = new ApplicationInstaller();
            Services = installer.Install();
            installed = true;

            Services.LobbyManager.OnPlayerJoined += HandlePlayerJoined;
            Services.LobbyManager.OnPlayerLeft += HandlePlayerLeft;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ApplicationBootstrap] Init error: {ex.Message}");
            throw;
        }
    }

    private void HandlePlayerJoined(PlayerSession player)
    {
        try
        {
            OnPlayerJoined?.Invoke(player);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ApplicationBootstrap] Error in HandlePlayerJoined: {e.Message}");
        }
    }

    private void HandlePlayerLeft(int playerId)
    {
        try
        {
            OnPlayerLeft?.Invoke(playerId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ApplicationBootstrap] Error in HandlePlayerLeft: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        try
        {
            if (Services?.LobbyManager != null)
            {
                Services.LobbyManager.OnPlayerJoined -= HandlePlayerJoined;
                Services.LobbyManager.OnPlayerLeft -= HandlePlayerLeft;
            }
        }
        catch { }
    }
}