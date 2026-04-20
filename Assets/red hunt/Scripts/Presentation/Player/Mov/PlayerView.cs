using Unity.Cinemachine;
using UnityEngine;

public class PlayerView : MonoBehaviour
{
    public int PlayerId { get; private set; }
    public bool IsHost { get; private set; }
    public bool IsLocal { get; private set; }

    [Header("Cinemachine - priorities")]
    [SerializeField] private int localPriority = 100;
    [SerializeField] private int fallbackPriority = 10;

    [Header("Direct reference")]
    [SerializeField] private CinemachineCamera vcam;

    private int cachedDefaultPriority;
    private bool defaultPriorityCached;
    private bool isLocalCameraActive;

    public void Init(int playerId, bool isHost)
    {
        PlayerId = playerId;
        IsHost = isHost;
    }

    public void SetLocal(bool isLocal)
    {
        IsLocal = isLocal;
    }

    private void Awake()
    {
        if (vcam == null)
            vcam = GetComponentInChildren<CinemachineCamera>(true);

        if (vcam != null)
        {
            try
            {
                cachedDefaultPriority = vcam.Priority;
                defaultPriorityCached = true;
            }
            catch
            {
                defaultPriorityCached = false;
            }
        }
        else
        {
            Debug.LogWarning("[PlayerView] CinemachineCamera not found in children or assigned in Inspector");
        }

        var camBootstrap = ModularLobbyBootstrap.Instance?.GetComponent<PlayerCameraBootstrap>();
        camBootstrap?.RegisterPlayerView(this);
    }

    private void OnDestroy()
    {
        var camBootstrap = ModularLobbyBootstrap.Instance?.GetComponent<PlayerCameraBootstrap>();
        camBootstrap?.UnregisterPlayerView(this);
    }

    public void ActivateLocalCamera()
    {
        if (vcam == null)
        {
            vcam = GetComponentInChildren<CinemachineCamera>(true);
            if (vcam == null)
            {
                Debug.LogWarning($"[PlayerView] ActivateLocalCamera: vcam not found for Player {PlayerId}");
                return;
            }
        }

        var go = vcam.gameObject;
        bool wasActive = go.activeSelf;
        if (!wasActive) go.SetActive(true);

        int before = defaultPriorityCached ? cachedDefaultPriority : -1;

        try
        {
            vcam.Priority = localPriority;
        }
        catch
        {
            Debug.LogWarning($"[PlayerView] Could not assign Priority in vcam (Player {PlayerId})");
        }

        isLocalCameraActive = true;
    }

    public void DeactivateLocalCamera()
    {
        if (vcam == null)
        {
            vcam = GetComponentInChildren<CinemachineCamera>(true);
            if (vcam == null)
            {
                Debug.LogWarning($"[PlayerView] DeactivateLocalCamera: vcam no encontrada para Player {PlayerId}");
                return;
            }
        }

        try
        {
            vcam.Priority = defaultPriorityCached ? cachedDefaultPriority : fallbackPriority;
        }
        catch
        {
            Debug.LogWarning($"[PlayerView] Could not restore Priority in vcam (Player {PlayerId})");
        }

        var go = vcam.gameObject;
        if (go.activeSelf)
            go.SetActive(false);

        isLocalCameraActive = false;
    }

    public bool IsLocalCameraActive() => isLocalCameraActive;
}