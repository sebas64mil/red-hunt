using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] private string ipAddress = "127.0.0.1";
    [SerializeField] private int port = 7777;

    public event Action<string, int> OnCreateLobby;
    public event Action<string, int> OnJoinLobby;

    public event Action OnLeaveLobby;

    private bool isHost;
    private bool isConnected;

    [SerializeField] private GameObject leaveButton;


    public void OnSelectKiller()
    {
        OnCreateLobby?.Invoke(ipAddress, port);
    }

    public void OnSelectEscapist()
    {
        OnJoinLobby?.Invoke(ipAddress, port);
    }

    public void OnLeaveButton()
    {
        OnLeaveLobby?.Invoke();
    }

    public void SetIsHost(bool isHost)
    {
        this.isHost = isHost;
        UpdateLeaveButton();
    }


    public void SetConnected(bool connected)
    {
        this.isConnected = connected;
        UpdateLeaveButton();
    }

    private void UpdateLeaveButton()
    {
        if (leaveButton != null)
            leaveButton.SetActive(isConnected && !isHost);
    }
}