using System;
using UnityEngine;

public class ModularLobbyBootstrap : MonoBehaviour
{
    public static ModularLobbyBootstrap Instance { get; private set; }

    [Header("Presentation")]    
    [SerializeField] private LobbyUI lobbyUI;
    [SerializeField] private SpawnUI spawnUI;
    [SerializeField] private AdminUI adminUI;

    // Referencias a bootstraps autónomos
    private ApplicationBootstrap appBoot;
    private NetworkBootstrap networkBoot;
    private PresentationBootstrap presentationBoot;
    private UIBindingBootstrap uiBinding;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log("[ModularBootstrap] Otra instancia detectada, destruyendo esta.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[ModularBootstrap] ============ INICIANDO SISTEMA (orquestador) ============");

        try
        {
            OrchestrateBootstraps();
            Debug.Log("[ModularBootstrap] SISTEMA ORQUESTADO CORRECTAMENTE");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ModularBootstrap] ERROR orquestando bootstraps: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private void OrchestrateBootstraps()
    {
        // 1) Application
        appBoot = gameObject.GetComponent<ApplicationBootstrap>() ?? gameObject.AddComponent<ApplicationBootstrap>();
        appBoot.Init();

        // 2) Network - NetworkBootstrap gestiona sus propios Server/Client (autónomo)
        networkBoot = gameObject.GetComponent<NetworkBootstrap>() ?? gameObject.AddComponent<NetworkBootstrap>();
        networkBoot.Init(appBoot.Services);

        // 3) Presentation
        presentationBoot = gameObject.GetComponent<PresentationBootstrap>() ?? gameObject.AddComponent<PresentationBootstrap>();
        presentationBoot.Init(lobbyUI, spawnUI);

        // Conectar Presentation a Application y Network para que gestione UI en respuesta a eventos
        presentationBoot.AttachApplication(appBoot);
        presentationBoot.AttachNetwork(networkBoot);

        // Registrar AdminUI para que Presentation actualice la lista si hace falta
        if (adminUI != null)
            presentationBoot.RegisterAdminUI(adminUI);

        // 4) UIBinding (gestiona eventos de UI y ejecuta flows usando Network/Presentation/Application)
        uiBinding = gameObject.GetComponent<UIBindingBootstrap>() ?? gameObject.AddComponent<UIBindingBootstrap>();
        uiBinding.Bind(lobbyUI, adminUI, networkBoot, appBoot, presentationBoot);
    }

    // -------------------- Registration API (delegan a bootstraps) --------------------
    public void RegisterLobbyUI(LobbyUI ui)
    {
        if (ui == null) return;
        lobbyUI = ui;

        // Delegar a PresentationBootstrap
        if (presentationBoot != null)
            presentationBoot.RegisterLobbyUI(ui);

        // Re-bind UIBinding para garantizar enlaces si Register se llama después de Bind
        if (uiBinding != null && appBoot != null && networkBoot != null && presentationBoot != null)
            uiBinding.Bind(lobbyUI, adminUI, networkBoot, appBoot, presentationBoot);

        Debug.Log("[ModularBootstrap] LobbyUI registrada (delegada)");
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
        Debug.Log("[ModularBootstrap] LobbyUI desregistrada (delegada)");
    }

    public void RegisterSpawnUI(SpawnUI ui)
    {
        if (ui == null) return;
        spawnUI = ui;

        if (presentationBoot != null)
            presentationBoot.RegisterSpawnUI(ui);

        Debug.Log("[ModularBootstrap] SpawnUI registrada (delegada)");
    }

    public void UnregisterSpawnUI(SpawnUI ui)
    {
        if (ui == null) return;
        if (spawnUI != ui) return;
        spawnUI = null;
        Debug.Log("[ModularBootstrap] SpawnUI desregistrada (delegada)");
    }

    public void RegisterAdminUI(AdminUI ui)
    {
        if (ui == null) return;
        adminUI = ui;

        if (presentationBoot != null)
            presentationBoot.RegisterAdminUI(adminUI);

        // Re-bind to ensure UIBinding subscribes admin events
        if (uiBinding != null && appBoot != null && networkBoot != null && presentationBoot != null)
            uiBinding.Bind(lobbyUI, adminUI, networkBoot, appBoot, presentationBoot);

        Debug.Log("[ModularBootstrap] AdminUI registrada (delegada)");
    }

    public void UnregisterAdminUI(AdminUI ui)
    {
        if (ui == null) return;
        if (adminUI != ui) return;

        if (presentationBoot != null)
            presentationBoot.RegisterAdminUI(null);

        adminUI = null;
        Debug.Log("[ModularBootstrap] AdminUI desregistrada (delegada)");
    }

    private void OnDestroy()
    {
        // No duplicar limpieza: cada bootstrap limpia sus propios handlers en OnDestroy.
        // Aquí sólo rompemos la singleton.
        if (Instance == this) Instance = null;
    }
}