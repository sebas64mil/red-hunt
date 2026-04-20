using System;
using UnityEngine;
using TMPro;

public class WinUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private LeaveButton leaveButton;
    [SerializeField] private ShutdownButton shutdownButton;

    [SerializeField] private string killerTitle = "Killer Wins!";
    [SerializeField] private string killerDescription = "The killer has eliminated all escapists.";

    [SerializeField] private string escapistTitle = "Escapists Win!";
    [SerializeField] private string escapistDescription = "The escapists managed to escape.";

    public event Action OnLeaveLobby;
    public event Action OnReturnToLobby;

    private bool isHost;
    private bool isConnected;

    private void Awake()
    {
        if (titleText == null)
            Debug.LogError("[WinUI] titleText not assigned in Inspector");
        if (descriptionText == null)
            Debug.LogError("[WinUI] descriptionText not assigned in Inspector");

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

    private void UpdateButtonVisibility()
    {
        bool showLeave = isConnected && !isHost;
        bool showReturn = isConnected && isHost;

        if (leaveButton != null)
        {
            leaveButton.SetVisible(showLeave);
        }

        if (shutdownButton != null)
        {
            shutdownButton.SetVisible(showReturn);
        }
    }

    private void OnLeaveButton()
    {
        OnLeaveLobby?.Invoke();
    }

    private void OnReturnToLobbyButton()
    {
        OnReturnToLobby?.Invoke();
    }

    public void SetWinInfo(string title, string description)
    {
        if (titleText != null)
        {
            titleText.text = title;
            titleText.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("[WinUI] titleText is null - cannot set title");
        }

        if (descriptionText != null)
        {
            descriptionText.text = description;
            descriptionText.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("[WinUI] descriptionText is null - cannot set description");
        }
    }

    public void SetKillerWin()
    {
        SetWinInfo(killerTitle, killerDescription);
    }

    public void SetEscapistWin()
    {
        SetWinInfo(escapistTitle, escapistDescription);
    }
}