using TMPro;
using UnityEngine;

public sealed class PlayerIdLabelUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerView playerView;
    [SerializeField] private TMP_Text label;

    [Header("Formato")]
    [SerializeField] private string format = "ID: {0}";

    private int lastPlayerId = int.MinValue;

    private void Awake()
    {
        if (playerView == null)
        {
            playerView = GetComponentInParent<PlayerView>();
        }

        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }

        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (playerView == null || label == null)
        {
            return;
        }

        int id = playerView.PlayerId;
        if (id == lastPlayerId)
        {
            return;
        }

        lastPlayerId = id;
        label.text = string.Format(format, id);
    }
}