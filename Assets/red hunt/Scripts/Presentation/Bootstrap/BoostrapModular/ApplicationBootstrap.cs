using System;
using UnityEngine;

public class ApplicationBootstrap : MonoBehaviour
{
    public ApplicationServices Services { get; private set; }

    // Eventos que representan la capa Application
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
            Debug.Log("[ApplicationBootstrap] ApplicationServices instalados.");

            // Suscribirse a los eventos del LobbyManager y reexponerlos
            Services.LobbyManager.OnPlayerJoined += HandlePlayerJoined;
            Services.LobbyManager.OnPlayerLeft += HandlePlayerLeft;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ApplicationBootstrap] Error Init: {ex.Message}");
            throw;
        }
    }

    private void HandlePlayerJoined(PlayerSession player)
    {
        try
        {
            Debug.Log($"[ApplicationBootstrap] PlayerJoined: {player.Id}");
            OnPlayerJoined?.Invoke(player);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ApplicationBootstrap] Error en HandlePlayerJoined: {e.Message}");
        }
    }

    private void HandlePlayerLeft(int playerId)
    {
        try
        {
            Debug.Log($"[ApplicationBootstrap] PlayerLeft: {playerId}");
            OnPlayerLeft?.Invoke(playerId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ApplicationBootstrap] Error en HandlePlayerLeft: {e.Message}");
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