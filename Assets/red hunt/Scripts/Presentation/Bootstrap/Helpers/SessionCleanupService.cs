using System;

public class SessionCleanupService
{
    private readonly PresentationServices presentationServices;
    private readonly AdminUI adminUI;

    public SessionCleanupService(
        PresentationServices presentationServices,
        AdminUI adminUI)
    {
        this.presentationServices = presentationServices;
        this.adminUI = adminUI;
    }

    public void CleanupAfterDisconnect()
    {
        try
        {
            presentationServices?.LobbyUI?.ResetAllToMain();
            presentationServices?.LobbyUI?.SetConnected(false);

            adminUI?.ClearAll();
            adminUI?.SetIsHost(false);
        }
        catch (Exception e)
        {
            DebugLog(e);
        }
    }

    public void CleanupAfterLeaveLobby()
    {
        try
        {
            presentationServices?.LobbyUI?.ResetAllToMain();
            presentationServices?.LobbyUI?.SetConnected(false);

            adminUI?.ClearAll();
        }
        catch (Exception e)
        {
            DebugLog(e);
        }
    }

    public void CleanupAfterShutdown()
    {
        try
        {
            presentationServices?.LobbyUI?.ResetAllToMain();
            presentationServices?.LobbyUI?.SetConnected(false);
            presentationServices?.LobbyUI?.SetIsHost(false);

            adminUI?.SetIsHost(false);
            adminUI?.ClearAll();
        }
        catch (Exception e)
        {
            DebugLog(e);
        }
    }

    private void DebugLog(Exception e)
    {
        UnityEngine.Debug.LogWarning($"[SessionCleanupService] {e.Message}");
    }
}