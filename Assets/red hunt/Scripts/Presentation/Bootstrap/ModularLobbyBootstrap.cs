using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ModularLobbyBootstrap : MonoBehaviour
{
    public static ModularLobbyBootstrap Instance { get; private set; }

    [Header("Presentation")]    
    [SerializeField] private LobbyUI lobbyUI;
    [SerializeField] private SpawnUI spawnUI;
    [SerializeField] private AdminUI adminUI;
    [SerializeField] private GameUI gameUI;

    public event Action<string> OnRequestStartGame;

    private ApplicationBootstrap appBoot;
    private NetworkBootstrap networkBoot;
    private PresentationBootstrap presentationBoot;
    private UIBindingBootstrap uiBinding;
    private GameplayBootstrap gameplayBootstrap;
    private WinBootstrap winBootstrap;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            OrchestrateBootstraps();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ModularLobbyBootstrap] Error orchestrating bootstraps: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private void OrchestrateBootstraps()
    {
        appBoot = gameObject.GetComponent<ApplicationBootstrap>() ?? gameObject.AddComponent<ApplicationBootstrap>();
        appBoot.Init();

        networkBoot = gameObject.GetComponent<NetworkBootstrap>() ?? gameObject.AddComponent<NetworkBootstrap>();
        networkBoot.Init(appBoot.Services, false);

        presentationBoot = gameObject.GetComponent<PresentationBootstrap>() ?? gameObject.AddComponent<PresentationBootstrap>();
        presentationBoot.Init(lobbyUI, spawnUI);

        presentationBoot.AttachApplication(appBoot);
        presentationBoot.AttachNetwork(networkBoot);

        if (adminUI != null)
            presentationBoot.RegisterAdminUI(adminUI);

        uiBinding = gameObject.GetComponent<UIBindingBootstrap>() ?? gameObject.AddComponent<UIBindingBootstrap>();
        uiBinding.Bind(lobbyUI, adminUI, networkBoot, appBoot, presentationBoot);

        presentationBoot.AttachUIBinding(uiBinding);

        if (uiBinding != null)
        {
            OnRequestStartGame -= uiBinding.HandleExternalStartRequest;
            OnRequestStartGame += uiBinding.HandleExternalStartRequest;
        }

        gameplayBootstrap = gameObject.AddComponent<GameplayBootstrap>();
        gameplayBootstrap.Init(networkBoot, appBoot, presentationBoot, gameUI);

        if (uiBinding != null)
            uiBinding.SetGameUI(gameUI);

        winBootstrap = gameObject.AddComponent<WinBootstrap>();
        winBootstrap.Init(networkBoot, appBoot, presentationBoot);
    }

    public void RequestStartGame(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[ModularLobbyBootstrap] RequestStartGame: sceneName is empty");
            return;
        }

        OnRequestStartGame?.Invoke(sceneName);
    }

    public LobbyManager GetLobbyManager()
    {
        try
        {
            return appBoot?.Services?.LobbyManager;
        }
        catch
        {
            return null;
        }
    }

    public LobbyNetworkService GetLobbyNetworkService()
    {
        try
        {
            return networkBoot?.GetLobbyNetworkService();
        }
        catch
        {
            return null;
        }
    }

    public GameNetworkService GetGameNetworkService()
    {
        try
        {
            return networkBoot?.GetGameNetworkService();
        }
        catch
        {
            return null;
        }
    }

    public void RegisterLobbyUI(LobbyUI ui)
    {
        if (ui == null) return;
        lobbyUI = ui;

        if (presentationBoot != null)
            presentationBoot.RegisterLobbyUI(ui);

        if (uiBinding != null && appBoot != null && networkBoot != null && presentationBoot != null)
            uiBinding.Bind(lobbyUI, adminUI, networkBoot, appBoot, presentationBoot);
    }

    public void UnregisterLobbyUI(LobbyUI ui)
    {
        if (ui == null) return;
        if (lobbyUI != ui) return;

        if (presentationBoot != null)
            presentationBoot.RegisterLobbyUI(null);

        if (uiBinding != null)
            uiBinding.Bind(null, adminUI, networkBoot, appBoot, presentationBoot);

        lobbyUI = null;
    }

    public void RegisterSpawnUI(SpawnUI ui)
    {
        if (ui == null) return;
        spawnUI = ui;

        if (presentationBoot != null)
            presentationBoot.RegisterSpawnUI(ui);
    }

    public void UnregisterSpawnUI(SpawnUI ui)
    {
        if (ui == null) return;
        if (spawnUI != ui) return;
        spawnUI = null;
    }

    public void RegisterAdminUI(AdminUI ui)
    {
        if (ui == null) return;
        adminUI = ui;

        if (presentationBoot != null)
            presentationBoot.RegisterAdminUI(adminUI);

        if (uiBinding != null && appBoot != null && networkBoot != null && presentationBoot != null)
            uiBinding.Bind(lobbyUI, adminUI, networkBoot, appBoot, presentationBoot);
    }

    public void UnregisterAdminUI(AdminUI ui)
    {
        if (ui == null) return;
        if (adminUI != ui) return;

        if (presentationBoot != null)
            presentationBoot.RegisterAdminUI(null);

        adminUI = null;
    }

    public void RegisterGameUI(GameUI ui)
    {
        if (ui == null) return;
        gameUI = ui;

        if (gameplayBootstrap != null)
            gameplayBootstrap.SetGameUI(gameUI);

        if (uiBinding != null)
            uiBinding.SetGameUI(gameUI);
    }

    public void UnregisterGameUI(GameUI ui)
    {
        if (ui == null) return;
        if (gameUI != ui) return;

        if (gameplayBootstrap != null)
            gameplayBootstrap.SetGameUI(null);

        if (uiBinding != null)
            uiBinding.SetGameUI(null);

        gameUI = null;
    }

    public void RegisterWinUI(WinUI ui)
    {
        if (ui == null) return;

        if (winBootstrap != null)
            winBootstrap.SetWinUI(ui);
    }

    public void RegisterWinCameraManager(WinCameraManager manager)
    {
        if (manager == null) return;

        if (winBootstrap != null)
            winBootstrap.SetWinCameraManager(manager);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}