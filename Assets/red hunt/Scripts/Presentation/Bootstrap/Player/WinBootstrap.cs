using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WinBootstrap : MonoBehaviour
{
    private NetworkBootstrap networkBootstrap;
    private ApplicationBootstrap applicationBootstrap;
    private PresentationBootstrap presentationBootstrap;

    private LobbyNetworkService lobbyNetworkService;
    private WinUI winUI;
    private WinCameraManager winCameraManager;

    private int winnerId = -1;
    private string winnerType = "";
    private bool isKillerWin = false;
    private bool hasUpdatedDisplay = false;  
    
    private int externalWinnerId = -1;
    private string externalWinnerType = "";
    private bool externalIsKillerWin = false;

    public void Init(NetworkBootstrap network, ApplicationBootstrap application, PresentationBootstrap presentation)
    {
        networkBootstrap = network ?? throw new ArgumentNullException(nameof(network));
        applicationBootstrap = application ?? throw new ArgumentNullException(nameof(application));
        presentationBootstrap = presentation ?? throw new ArgumentNullException(nameof(presentation));

        lobbyNetworkService = network.GetLobbyNetworkService();

        SceneManager.sceneLoaded += HandleSceneLoaded;

    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Win") return;

        hasUpdatedDisplay = false;
        SubscribeToWinEvent();
    }

    public void SetWinUIAndCamera(WinUI ui, WinCameraManager camera)
    {
        winUI = ui ?? throw new ArgumentNullException(nameof(ui));
        winCameraManager = camera ?? throw new ArgumentNullException(nameof(camera));
        

        BindWinUIEvents();
        
        if (winnerId > 0 && !hasUpdatedDisplay)
        {
            UpdateWinDisplay();
        }
    }

    public void SetWinUI(WinUI ui)
    {
        winUI = ui ?? throw new ArgumentNullException(nameof(ui));

        BindWinUIEvents();
        
        if (!hasUpdatedDisplay && winCameraManager != null)
        {
            UpdateWinDisplay();
        }
    }

    public void SetWinCameraManager(WinCameraManager manager)
    {
        winCameraManager = manager ?? throw new ArgumentNullException(nameof(manager));
        Debug.Log("[WinBootstrap] ✅ WinCameraManager asignado");

        if (!hasUpdatedDisplay && winUI != null)
        {
            UpdateWinDisplay();
        }
    }

    public void SetWinData(int winnerId, string winnerType, bool isKillerWin)
    {
        
        this.externalWinnerId = winnerId;
        this.externalWinnerType = winnerType;
        this.externalIsKillerWin = isKillerWin;

        if (!hasUpdatedDisplay && winUI != null && winCameraManager != null)
        {
            hasUpdatedDisplay = false; 
            UpdateWinDisplay();
        }
    }

    private void SubscribeToWinEvent()
    {
        if (lobbyNetworkService != null)
        {
            lobbyNetworkService.OnGameWinReceived += HandleGameWin;
        }
        else
        {
            Debug.LogError("[WinBootstrap] ❌ lobbyNetworkService es NULL");
        }
    }

    private void BindWinUIEvents()
    {
        if (winUI == null) return;

        UnbindWinUIEvents();

        winUI.OnLeaveLobby -= HandleLeaveLobby;
        winUI.OnLeaveLobby += HandleLeaveLobby;

        winUI.OnReturnToLobby -= HandleReturnToLobby;
        winUI.OnReturnToLobby += HandleReturnToLobby;

        var isHost = networkBootstrap?.Services?.ClientState?.IsHost ?? false;
        winUI.SetIsHost(isHost);
        winUI.SetConnected(true);

    }

    private void UnbindWinUIEvents()
    {
        if (winUI == null) return;
        try
        {
            winUI.OnLeaveLobby -= HandleLeaveLobby;
            winUI.OnReturnToLobby -= HandleReturnToLobby;
        }
        catch { }
    }

    private async void HandleLeaveLobby()
    {

        try
        {
            await lobbyNetworkService?.LeaveLobby();

            GameManager.SetCursorVisible(true);
            GameManager.ChangeScene("Lobby");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WinBootstrap] ⚠️ Error en HandleLeaveLobby: {e.Message}");
        }
    }

    private async void HandleReturnToLobby()
    {
        Debug.Log("[WinBootstrap] 🔄 Host presionó Return to Lobby desde Win");

        try
        {
            if (lobbyNetworkService != null)
            {
                lobbyNetworkService.ResetGameStarted();
                await lobbyNetworkService.ReturnAllPlayersToLobby();
            }

            GameManager.ChangeScene("Lobby");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WinBootstrap] ⚠️ Error en HandleReturnToLobby: {e.Message}");
        }
    }

    private void HandleGameWin(int winnerId, string winnerType, bool isKillerWin)
    {

        this.winnerId = winnerId;
        this.winnerType = winnerType;
        this.isKillerWin = isKillerWin;

        if (winCameraManager != null)
        {
            hasUpdatedDisplay = false;
            UpdateWinDisplay();
            Debug.Log("[WinBootstrap] ✅ Actualización desde HandleGameWin exitosa");
        }
        else
        {
            Debug.Log("[WinBootstrap] ⏳ WinCameraManager aún no asignado, esperando SetWinUIAndCamera()...");
        }
    }

    private void UpdateWinDisplay()
    {
        if (hasUpdatedDisplay)
        {
            return;
        }

        if (winCameraManager == null)
        {
            Debug.LogError("[WinBootstrap] ❌ CRÍTICO: winCameraManager es NULL en UpdateWinDisplay()");
            return;
        }

        if (winUI == null)
        {
            Debug.LogError("[WinBootstrap] ❌ CRÍTICO: winUI es NULL en UpdateWinDisplay()");
            return;
        }

        bool killerWins = false;
        
        if (winnerId > 0)
        {
            killerWins = isKillerWin;
        }
        else if (externalWinnerId > 0)
        {
            killerWins = externalIsKillerWin;
        }
        else
        {
            Debug.LogError("[WinBootstrap] ❌ No hay datos de victoria (ni externos ni internos)");
            return;
        }



        
        if (killerWins)
        {
            winCameraManager.ShowKillerCamera();
            winUI.SetKillerWin();
        }
        else
        {
            winCameraManager.ShowEscapistCamera();
            winUI.SetEscapistWin();
        }

        hasUpdatedDisplay = true;
    }

    private void OnDestroy()
    {
        try
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (lobbyNetworkService != null)
                lobbyNetworkService.OnGameWinReceived -= HandleGameWin;

            UnbindWinUIEvents();
        }
        catch { }
    }
}
