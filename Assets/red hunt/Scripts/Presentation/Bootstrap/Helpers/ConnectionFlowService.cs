using System.Threading.Tasks;

public class ConnectionFlowService
{
    private readonly NetworkServices networkServices;
    private readonly LobbyNetworkService lobbyNetworkService;
    private readonly ApplicationServices applicationServices;
    private readonly PresentationServices presentationServices;

    public bool IsServerStarted { get; private set; }
    public bool IsClientConnected { get; private set; }

    public ConnectionFlowService(
        NetworkServices networkServices,
        LobbyNetworkService lobbyNetworkService,
        ApplicationServices applicationServices,
        PresentationServices presentationServices)
    {
        this.networkServices = networkServices;
        this.lobbyNetworkService = lobbyNetworkService;
        this.applicationServices = applicationServices;
        this.presentationServices = presentationServices;
    }

    public async Task StartHostAsync(int port)
    {
        if (IsServerStarted) return;

        await networkServices.Server.StartServer(port);
        IsServerStarted = true;

        lobbyNetworkService.SetIsHost(true);
        presentationServices.LobbyUI.SetIsHost(true);
        presentationServices.LobbyUI.SetConnected(true);

        networkServices.AdminService.SetIsHost(true);
        networkServices.SwitchToHost(applicationServices.LobbyManager);
    }

    public async Task<bool> StartClientAsync(string ip, int port)
    {
        if (IsClientConnected) return true;

        bool success = await networkServices.Client.ConnectToServer(ip, port);
        if (!success)
        {
            IsClientConnected = false;
            return false;
        }

        IsClientConnected = true;

        lobbyNetworkService.SetIsHost(false);
        presentationServices.LobbyUI.SetIsHost(false);
        presentationServices.LobbyUI.SetConnected(true);

        networkServices.AdminService.SetIsHost(false);
        networkServices.SwitchToClient(applicationServices.LobbyManager);

        return true;
    }

    public void DisconnectClient()
    {
        try
        {
            networkServices.Client?.Disconnect();
        }
        catch
        {
            // avoid throwing from here; orchestrator will handle logging if needed
        }
        IsClientConnected = false;
    }
    public async Task ShutdownServerAsync()
    {
        try
        {
            await Task.Run(() => networkServices.Server?.Disconnect());
        }
        catch
        {
        }

        IsServerStarted = false;
        IsClientConnected = false;
    }
}