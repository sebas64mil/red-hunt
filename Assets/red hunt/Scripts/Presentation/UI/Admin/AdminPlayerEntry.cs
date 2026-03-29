using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AdminPlayerEntry : MonoBehaviour
{
    [SerializeField] private TMP_Text idText;
    [SerializeField] private Button kickButton;

    private int playerId;
    private Action<int> onKick;

    public void Setup(int id, Action<int> onKick)
    {
        playerId = id;
        this.onKick = onKick ?? throw new ArgumentNullException(nameof(onKick));

        if (idText != null)
            idText.text = $"ID {playerId}";

        if (kickButton != null)
        {
            kickButton.onClick.RemoveAllListeners();
            kickButton.onClick.AddListener(() => this.onKick?.Invoke(playerId));

            kickButton.interactable = playerId != 1;

        }
    }

    public int PlayerId => playerId;
}