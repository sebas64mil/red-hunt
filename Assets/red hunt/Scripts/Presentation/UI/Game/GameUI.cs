using System;
using UnityEngine;

public class GameUI : MonoBehaviour
{
    [SerializeField] private LeaveButton leaveButton;
    [SerializeField] private ShutdownButton shutdownButton;

    public event Action OnLeaveLobby;
    public event Action OnReturnToLobby;

    private bool isHost;
    private bool isConnected;
    private LobbyUI lobbyUI;

    private void Awake()
    {
        if (leaveButton != null)
        {
            leaveButton.OnClicked -= OnLeaveButton;
            leaveButton.OnClicked += OnLeaveButton;
        }

        if (shutdownButton != null)
        {
            shutdownButton.OnClicked -= OnReturnToLobbyButton;
            shutdownButton.OnClicked += OnReturnToLobbyButton;
        }

        UpdateButtonVisibility();
    }

    private void OnDestroy()
    {
        if (leaveButton != null) leaveButton.OnClicked -= OnLeaveButton;
        if (shutdownButton != null) shutdownButton.OnClicked -= OnReturnToLobbyButton;
    }

    public void SetIsHost(bool host)
    {
        isHost = host;
        UpdateButtonVisibility();
    }

    public void SetConnected(bool connected)
    {
        isConnected = connected;
        UpdateButtonVisibility();
    }

    public void SetLobbyUI(LobbyUI lobbyUIReference)
    {
        lobbyUI = lobbyUIReference;
    }

    private void UpdateButtonVisibility()
    {
        if (leaveButton != null)
            leaveButton.SetVisible(isConnected && !isHost);

        if (shutdownButton != null)
            shutdownButton.SetVisible(isConnected && isHost);
    }

    private void OnLeaveButton()
    {
        OnLeaveLobby?.Invoke();
    }

    private void OnReturnToLobbyButton()
    {
        OnReturnToLobby?.Invoke();
    }
}