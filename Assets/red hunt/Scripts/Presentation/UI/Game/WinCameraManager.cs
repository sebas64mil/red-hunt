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
            Debug.Log("[WinCameraManager] ?? Mostrando cámara del Killer");
        }
        else
        {
            Debug.LogWarning("[WinCameraManager] ?? killerCameraObject no asignado");
        }

        if (escapistCameraObject != null)
            escapistCameraObject.SetActive(false);
    }

    public void ShowEscapistCamera()
    {
        if (escapistCameraObject != null)
        {
            escapistCameraObject.SetActive(true);
            Debug.Log("[WinCameraManager] ?? Mostrando cámara del Escapist");
        }
        else
        {
            Debug.LogWarning("[WinCameraManager] ?? escapistCameraObject no asignado");
        }

        if (killerCameraObject != null)
            killerCameraObject.SetActive(false);
    }

}