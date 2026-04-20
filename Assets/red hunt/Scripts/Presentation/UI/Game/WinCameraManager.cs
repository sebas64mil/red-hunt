using UnityEngine;

public class WinCameraManager : MonoBehaviour
{
    [SerializeField] private GameObject killerCameraObject;
    [SerializeField] private GameObject escapistCameraObject;

    public void ShowKillerCamera()
    {
        if (killerCameraObject != null)
        {
            killerCameraObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[WinCameraManager] killerCameraObject not assigned");
        }

        if (escapistCameraObject != null)
            escapistCameraObject.SetActive(false);
    }

    public void ShowEscapistCamera()
    {
        if (escapistCameraObject != null)
        {
            escapistCameraObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[WinCameraManager] escapistCameraObject not assigned");
        }

        if (killerCameraObject != null)
            killerCameraObject.SetActive(false);
    }

}