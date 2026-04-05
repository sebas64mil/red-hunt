using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AdminPlayerEntry : MonoBehaviour
{
    [SerializeField] private TMP_Text idText;
    [SerializeField] private TMP_Text latencyText;
    [SerializeField] private Button kickButton;

    private int playerId;
    private Action<int> onKick;
    private int currentLatency = 0;

    public void Setup(int id, Action<int> onKick, int latency = 0)
    {
        playerId = id;
        this.onKick = onKick ?? throw new ArgumentNullException(nameof(onKick));

        if (idText != null)
            idText.text = $"ID {playerId}";

        if (latencyText != null)
            latencyText.text = $"{latency}ms";

        currentLatency = latency;

        if (kickButton != null)
        {
            kickButton.onClick.RemoveAllListeners();
            kickButton.onClick.AddListener(() => this.onKick?.Invoke(playerId));

            kickButton.interactable = playerId != 1;
        }
    }

    public void UpdateLatency(int latency)
    {
        currentLatency = latency;
        if (latencyText != null)
            latencyText.text = $"{latency}ms";
    }

    public int PlayerId => playerId;
    public int CurrentLatency => currentLatency;
}
