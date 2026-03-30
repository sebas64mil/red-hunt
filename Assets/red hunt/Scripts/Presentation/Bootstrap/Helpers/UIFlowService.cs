public class UIFlowService
{
    private readonly PresentationServices presentationServices;
    private readonly AdminUI adminUI;

    public UIFlowService(PresentationServices presentationServices, AdminUI adminUI)
    {
        this.presentationServices = presentationServices;
        this.adminUI = adminUI;
    }

    public void ShowLobby()
    {
        presentationServices?.LobbyUI?.ShowLobbyPanel();
        presentationServices?.LobbyUI?.SetConnected(true);
    }

    public void SetupHostUI()
    {
        presentationServices?.LobbyUI?.SetIsHost(true);
        adminUI?.SetIsHost(true);
        presentationServices?.LobbyUI?.SetConnected(true);
    }

    public void SetupClientUI()
    {
        presentationServices?.LobbyUI?.SetIsHost(false);
        adminUI?.SetIsHost(false);
        presentationServices?.LobbyUI?.SetConnected(true);
    }

    public void ResetAll()
    {
        presentationServices?.LobbyUI?.ResetAllToMain();
        adminUI?.ClearAll();
    }
}