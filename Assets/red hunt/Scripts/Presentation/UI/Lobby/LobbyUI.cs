using System;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] private string ipAddress = "127.0.0.1";
    [SerializeField] private int port = 7777;
    public string IpAddress => ipAddress;
    public int Port => port;

    public event Action<string, int> OnCreateLobby;
    public event Action<string, int> OnJoinLobby;

    public event Action OnLeaveLobby;
    public event Action OnShutdownServer;

    public event Action OnHostChosen;
    public event Action OnJoinChosen;
    public event Action<PlayerType> OnRoleChosen;
    public event Action<PlayerType> OnConfirmRole;
    public event Action<int> OnPlayerReady;
    public event Action OnStartGame;

    private bool isHost;
    private bool isConnected;

    [Header("Buttons (separated)")]
    [SerializeField] private LeaveButton leaveButton;
    [SerializeField] private ShutdownButton shutdownButton;

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject rolePanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Role Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button chooseKillerButton;
    [SerializeField] private Button chooseEscapistButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;

    [Header("Aux Buttons")]
    [SerializeField] private Button roleBackButton;


    private int localPlayerId = -1;
    private PlayerType? selectedRole = null;

    private bool readyProcessing = false;

    private bool everConnected = false;


    private void Awake()
    {
        ShowMainPanel();

        if (hostButton != null) hostButton.onClick.RemoveAllListeners();
        if (joinButton != null) joinButton.onClick.RemoveAllListeners();
        if (chooseKillerButton != null) chooseKillerButton.onClick.RemoveAllListeners();
        if (chooseEscapistButton != null) chooseEscapistButton.onClick.RemoveAllListeners();
        if (readyButton != null) readyButton.onClick.RemoveAllListeners();
        if (startButton != null) startButton.onClick.RemoveAllListeners();

        if (roleBackButton != null) roleBackButton.onClick.RemoveAllListeners();

        if (hostButton != null) hostButton.onClick.AddListener(OnHostButton);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinButton);
        if (chooseKillerButton != null) chooseKillerButton.onClick.AddListener(() => OnChooseRole(PlayerType.Killer));
        if (chooseEscapistButton != null) chooseEscapistButton.onClick.AddListener(() => OnChooseRole(PlayerType.Escapist));
        if (readyButton != null) readyButton.onClick.AddListener(OnReadyButton);
        if (startButton != null) startButton.onClick.AddListener(OnStartButton);

        if (leaveButton != null)
        {
            leaveButton.OnClicked -= OnLeaveButton;
            leaveButton.OnClicked += OnLeaveButton;
        }

        if (shutdownButton != null)
        {
            shutdownButton.OnClicked -= OnShutdownButton;
            shutdownButton.OnClicked += OnShutdownButton;
        }

        if (roleBackButton != null) roleBackButton.onClick.AddListener(OnBackToMain);

        if (readyButton != null)
            readyButton.interactable = false;
    }

    private void OnEnable()
    {
        try
        {
            LobbyBootstrap.Instance?.RegisterLobbyUI(this);
        }
        catch (Exception)
        {
            Debug.LogWarning("[LobbyUI] Registro con GameBootstrap falló");
        }
    }

    private void OnDisable()
    {
        try
        {
            LobbyBootstrap.Instance?.UnregisterLobbyUI(this);
        }
        catch (Exception) { }
    }

    private void OnDestroy()
    {
        if (leaveButton != null) leaveButton.OnClicked -= OnLeaveButton;
        if (shutdownButton != null) shutdownButton.OnClicked -= OnShutdownButton;
    }

    public void OnHostButton()
    {
        ShowRolePanel();
        OnHostChosen?.Invoke();
    }

    public void OnJoinButton()
    {
        ShowRolePanel();
        OnJoinChosen?.Invoke();
    }

    public void OnChooseRole(PlayerType role)
    {
        selectedRole = role;
        OnRoleChosen?.Invoke(role);

        if (readyButton != null && !readyProcessing)
            readyButton.interactable = true;
    }

    public void OnChooseKiller()
    {
        OnChooseRole(PlayerType.Killer);
    }

    public void OnChooseEscapist()
    {
        OnChooseRole(PlayerType.Escapist);
    }

    public void OnReadyButton()
    {
        if (readyProcessing)
        {
            Debug.Log("[LobbyUI] Ready ya en proceso, ignorando pulsación duplicada");
            return;
        }

        if (selectedRole == null)
        {
            Debug.LogWarning("[LobbyUI] Ready pulsado pero no se ha seleccionado rol");
            return;
        }

        readyProcessing = true;
        if (readyButton != null) readyButton.interactable = false;

        if (localPlayerId <= 0)
        {
            OnConfirmRole?.Invoke(selectedRole.Value);
            return;
        }

        OnPlayerReady?.Invoke(localPlayerId);
    }

    public void ResetReadyState()
    {
        readyProcessing = false;
        if (readyButton != null)
            readyButton.interactable = selectedRole != null;
    }

    public void ResetAllToMain()
    {
        selectedRole = null;
        readyProcessing = false;
        localPlayerId = -1;
        everConnected = false;

        SetIsHost(false);
        SetConnected(false);

        ShowMainPanel();
        ResetReadyState();
    }


    public void OnStartButton()
    {
        OnStartGame?.Invoke();
    }

    public void ConfirmCreateLobby()
    {
        OnCreateLobby?.Invoke(ipAddress, port);
    }

    public void ConfirmJoinLobby()
    {
        OnJoinLobby?.Invoke(ipAddress, port);
    }

    public void OnLeaveButton()
    {
        OnLeaveLobby?.Invoke();

        ShowRolePanel();

        selectedRole = null;
        readyProcessing = false;
        localPlayerId = -1;

        ResetReadyState();
        SetConnected(false);
    }

    public void OnShutdownButton()
    {
        OnShutdownServer?.Invoke();

        ShowRolePanel();

        selectedRole = null;
        readyProcessing = false;
        localPlayerId = -1;

        ResetReadyState();
        SetIsHost(false);
        SetConnected(false);
    }
    public void OnBackToMain()
    {
        ShowMainPanel();
        selectedRole = null;
        ResetReadyState();
    }

    public void SetIsHost(bool isHost)
    {
        this.isHost = isHost;
        UpdateStartButton();
        UpdateLeaveButton();
    }

    public void SetConnected(bool connected)
    {
        bool wasConnected = this.isConnected;
        this.isConnected = connected;

        if (connected)
        {
            everConnected = true;
        }
        else
        {
            if (everConnected && wasConnected)
            {
                selectedRole = null;
                readyProcessing = false;
                localPlayerId = -1;
                everConnected = false;

                ResetReadyState();
            }
        }

        UpdateLeaveButton();
    }

    public void SetLocalPlayerId(int id)
    {
        localPlayerId = id;
    }

    public void SetAllPlayersReady(bool allReady)
    {
        if (startButton != null)
            startButton.interactable = isHost && allReady;
    }

    public void ShowMainPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (rolePanel != null) rolePanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
    }

    public void ShowRolePanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (rolePanel != null) rolePanel.SetActive(true);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);

        if (readyButton != null)
            readyButton.interactable = selectedRole != null && !readyProcessing;
    }

    public void ShowLobbyPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (rolePanel != null) rolePanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
    }

    public void SetStartInteractable(bool enabled)
    {
        if (startButton == null) return;

        startButton.gameObject.SetActive(isHost);
        startButton.interactable = isHost && enabled;
    }

    private void UpdateLeaveButton()
    {
        if (leaveButton != null)
            leaveButton.SetVisible(isConnected && !isHost);

        if (shutdownButton != null)
            shutdownButton.SetVisible(isConnected && isHost);
    }

    private void UpdateStartButton()
    {
        if (startButton == null) return;

        startButton.gameObject.SetActive(isHost);
        startButton.interactable = false;
    }
}