using Unity.Cinemachine;
using UnityEngine;

public class PlayerView : MonoBehaviour
{
    public int PlayerId { get; private set; }
    public bool IsHost { get; private set; }
    public bool IsLocal { get; private set; }

    [Header("Cinemachine - prioridades")]
    [SerializeField] private int localPriority = 100;
    [SerializeField] private int fallbackPriority = 10;

    [Header("Referencia directa ")]
    [SerializeField] private CinemachineCamera vcam;

    private int cachedDefaultPriority;
    private bool defaultPriorityCached;
    private bool isLocalCameraActive;

    public void Init(int playerId, bool isHost)
    {
        PlayerId = playerId;
        IsHost = isHost;
    }

    // Solo actualiza el flag; el PlayerCameraBootstrap decide cuándo activar/desactivar
    public void SetLocal(bool isLocal)
    {
        IsLocal = isLocal;
    }

    private void Awake()
    {
        // Si no fue asignado por el Inspector, intentar encontrarlo (fallback)
        if (vcam == null)
            vcam = GetComponentInChildren<CinemachineCamera>(true);

        if (vcam != null)
        {
            // Cachear prioridad por si necesitamos restaurarla
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
            Debug.LogWarning("[PlayerView] No se encontró CinemachineCamera en los hijos ni asignada en el Inspector.");
        }

        // Registro en bootstrap persistente
        var camBootstrap = ModularLobbyBootstrap.Instance?.GetComponent<PlayerCameraBootstrap>();
        camBootstrap?.RegisterPlayerView(this);
    }

    private void OnDestroy()
    {
        var camBootstrap = ModularLobbyBootstrap.Instance?.GetComponent<PlayerCameraBootstrap>();
        camBootstrap?.UnregisterPlayerView(this);
    }

    // Métodos públicos que el bootstrap llamará según la escena / estado local
    public void ActivateLocalCamera()
    {
        if (vcam == null)
        {
            vcam = GetComponentInChildren<CinemachineCamera>(true);
            if (vcam == null)
            {
                Debug.LogWarning($"[PlayerView] ActivateLocalCamera: vcam no encontrada para Player {PlayerId}");
                return;
            }
        }

        // Asegurar que el GameObject de la vCam está activo: si estaba desactivado, la prioridad no se aplicará visiblemente
        var go = vcam.gameObject;
        bool wasActive = go.activeSelf;
        if (!wasActive) go.SetActive(true);

        // Registrar prioridad anterior para diagnóstico
        int before = defaultPriorityCached ? cachedDefaultPriority : -1;

        // Aplicar prioridad local
        try
        {
            vcam.Priority = localPriority;
        }
        catch
        {
            Debug.LogWarning($"[PlayerView] No se pudo asignar Priority en vcam (Player {PlayerId}).");
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

        // Restaurar prioridad
        try
        {
            vcam.Priority = defaultPriorityCached ? cachedDefaultPriority : fallbackPriority;
        }
        catch
        {
            Debug.LogWarning($"[PlayerView] No se pudo restaurar Priority en vcam (Player {PlayerId}).");
        }

        // Desactivar GameObject de la vCam para que no interfiera visualmente
        var go = vcam.gameObject;
        if (go.activeSelf)
            go.SetActive(false);

        isLocalCameraActive = false;
    }

    // Estado consultable
    public bool IsLocalCameraActive() => isLocalCameraActive;
}