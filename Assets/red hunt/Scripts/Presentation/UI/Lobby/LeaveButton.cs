using System;
using UnityEngine;
using UnityEngine.UI;

public class LeaveButton : MonoBehaviour
{
    [SerializeField] private Button button;

    public event Action OnClicked;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked?.Invoke());
        }
    }

    public void SetVisible(bool visible)
    {
        if (button != null)
            button.gameObject.SetActive(visible);
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveAllListeners();
    }
}