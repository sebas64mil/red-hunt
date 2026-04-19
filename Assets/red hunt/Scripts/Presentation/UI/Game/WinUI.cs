using System;
using UnityEngine;
using TMPro;

public class WinUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;    
    [SerializeField] private TextMeshProUGUI descriptionText; 
    [SerializeField] private LeaveButton leaveButton;
    [SerializeField] private ShutdownButton shutdownButton;

    // ⭐ NUEVO: Textos configurables para el asesino
    [SerializeField] private string killerTitle = "¡EL ASESINO GANA!";
    [SerializeField] private string killerDescription = "El asesino ha eliminado a todos los escapistas.";

    // ⭐ NUEVO: Textos configurables para los escapistas
    [SerializeField] private string escapistTitle = "¡LOS ESCAPISTAS GANAN!";
    [SerializeField] private string escapistDescription = "Los escapistas lograron escapar.";

    public event Action OnLeaveLobby;
    public event Action OnReturnToLobby;

    private bool isHost;
    private bool isConnected;

    private void Awake()
    {
        Debug.Log("[WinUI] ✅ Inicializado");
        
        // ⭐ NUEVO: Validar que los TextMeshPro existan
        if (titleText == null)
            Debug.LogError("[WinUI] ❌ titleText no asignado en el Inspector");
        if (descriptionText == null)
            Debug.LogError("[WinUI] ❌ descriptionText no asignado en el Inspector");

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
        Debug.Log($"[WinUI] SetIsHost={host}");
        UpdateButtonVisibility();
    }

    public void SetConnected(bool connected)
    {
        isConnected = connected;
        Debug.Log($"[WinUI] SetConnected={connected}");
        UpdateButtonVisibility();
    }

    private void UpdateButtonVisibility()
    {
        bool showLeave = isConnected && !isHost;
        bool showReturn = isConnected && isHost;

        if (leaveButton != null)
        {
            leaveButton.SetVisible(showLeave);
            Debug.Log($"[WinUI] LeaveButton.SetVisible({showLeave}) - isConnected={isConnected}, isHost={isHost}");
        }

        if (shutdownButton != null)
        {
            shutdownButton.SetVisible(showReturn);
            Debug.Log($"[WinUI] ShutdownButton.SetVisible({showReturn}) - isConnected={isConnected}, isHost={isHost}");
        }
    }

    private void OnLeaveButton()
    {
        Debug.Log("[WinUI] 👉 Cliente presionó Leave - abandonando el servidor");
        OnLeaveLobby?.Invoke();
    }

    private void OnReturnToLobbyButton()
    {
        Debug.Log("[WinUI] 👉 Host presionó Return to Lobby");
        OnReturnToLobby?.Invoke();
    }

    public void SetWinInfo(string title, string description)
    {
        if (titleText != null)
        {
            titleText.text = title;
            titleText.gameObject.SetActive(true);
            Debug.Log($"[WinUI] ✅ Título establecido: {title}");
        }
        else
        {
            Debug.LogError("[WinUI] ❌ titleText es NULL - no se puede establecer título");
        }

        if (descriptionText != null)
        {
            descriptionText.text = description;
            descriptionText.gameObject.SetActive(true);
            Debug.Log($"[WinUI] ✅ Descripción establecida: {description}");
        }
        else
        {
            Debug.LogError("[WinUI] ❌ descriptionText es NULL - no se puede establecer descripción");
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