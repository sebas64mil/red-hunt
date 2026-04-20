using UnityEngine;

public class RemotePlayerSync : MonoBehaviour
{
    [Header("Interpolation")]
    [SerializeField] private float positionLerpSpeed = 5f;
    [SerializeField] private float rotationLerpSpeed = 5f;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetVelocity;

    private int remotePlayerId = -1;
    private bool hasReceivedData = false;
    private bool isInitialized = false;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        if (rb != null)
        {
            rb.useGravity = true;
        }
        
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    public void Init(int playerId)
    {
        remotePlayerId = playerId;
        isInitialized = true;
        
        if (rb != null)
        {
            rb.useGravity = true;
        }
    }

    private void FixedUpdate()
    {
        if (!hasReceivedData || rb == null) return;

        InterpolatePosition();
        InterpolateRotation();
        ApplyNetworkVelocity();
    }

    public void OnRemotePositionReceived(MovePacket movePacket)
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"[RemotePlayerSync] OnRemotePositionReceived but NOT INITIALIZED: movePacket.playerId={movePacket.playerId}, remotePlayerId={remotePlayerId}");
            return;
        }

        if (movePacket.playerId != remotePlayerId)
        {
            Debug.LogWarning($"[RemotePlayerSync] MISMATCH: movePacket.playerId={movePacket.playerId} != remotePlayerId={remotePlayerId}");
            return;
        }

        targetPosition = movePacket.GetPosition();
        targetRotation = movePacket.GetRotation();
        targetVelocity = movePacket.GetVelocity();

        hasReceivedData = true;

        UpdateAnimations(movePacket);
    }

    private void InterpolatePosition()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.fixedDeltaTime * positionLerpSpeed);
    }

    private void InterpolateRotation()
    {
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationLerpSpeed);
    }

    private void ApplyNetworkVelocity()
    {
        if (rb != null)
        {
            Vector3 horizontalVelocity = new Vector3(targetVelocity.x, 0, targetVelocity.z);
            Vector3 currentVelocity = rb.linearVelocity;

            rb.linearVelocity = new Vector3(
                Mathf.Lerp(currentVelocity.x, horizontalVelocity.x, Time.fixedDeltaTime * positionLerpSpeed),
                currentVelocity.y,  
                Mathf.Lerp(currentVelocity.z, horizontalVelocity.z, Time.fixedDeltaTime * positionLerpSpeed)
            );
        }
    }

    private void UpdateAnimations(MovePacket movePacket)
    {
        float speed = targetVelocity.magnitude;
    }

    public int GetRemotePlayerId() => remotePlayerId;
}
