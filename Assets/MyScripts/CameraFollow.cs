using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Camera Position (world space, does NOT rotate with the player)")]
    [SerializeField]
    private Vector3 offset = new Vector3(0f, 4f, -7f);

    [Header("Position Smoothing (blend = 1 - e^(-k*dt))")]
    [SerializeField] private float sharpness = 5f;

    [Header("Look Ahead (seconds of velocity)")]
    [SerializeField] private float lookAhead = 0.35f;

    [Header("Rotation Smoothing")]
    [SerializeField] private float rotationSharpness = 8f;

    [Header("Optional Camera Boundaries (leave empty for the offset test)")]
    [SerializeField] private Collider roomBounds;
    [SerializeField] private float wallPadding = 0.5f;

    private Rigidbody targetRb;
    private Vector3 smoothedAimPoint;

    private void Awake()
    {
        FindTarget();
    }

    // Finds the player if the target is empty, then snaps the camera to it.
    private void FindTarget()
    {
        if (target == null)
        {
            PlayerController player =
                FindAnyObjectByType<PlayerController>();

            if (player != null)
                target = player.transform;
        }

        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();

            smoothedAimPoint = target.position;

            transform.position = ClampToRoom(target.position + offset);
            transform.LookAt(target.position);
        }
    }

    private void LateUpdate()
    {
        // The player may be spawned after the camera: keep looking for it.
        if (target == null)
            FindTarget();

        if (target == null)
            return;

        // Frame-rate independent blends.
        float positionBlend =
            1f - Mathf.Exp(-sharpness * Time.deltaTime);

        float rotationBlend =
            1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);

        // Flat velocity for look-ahead.
        Vector3 flatVelocity = Vector3.zero;

        if (targetRb != null)
        {
#if UNITY_6000_0_OR_NEWER
            flatVelocity = targetRb.linearVelocity;
#else
            flatVelocity = targetRb.velocity;
#endif
            flatVelocity.y = 0f;
        }

        // Aim where the player is going to be.
        Vector3 aimPoint = target.position + flatVelocity * lookAhead;

        smoothedAimPoint = Vector3.Lerp(
            smoothedAimPoint,
            aimPoint,
            positionBlend
        );

        // Fixed world-space offset: W always walks away from the camera
        // because the player's movement is built from the camera's axes.
        Vector3 wantedPosition = smoothedAimPoint + offset;

        Vector3 smoothPosition = Vector3.Lerp(
            transform.position,
            wantedPosition,
            positionBlend
        );

        transform.position = ClampToRoom(smoothPosition);

        // Look at the aim point, not the player, so look-ahead is not undone.
        Vector3 lookDirection = smoothedAimPoint - transform.position;

        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion wantedRotation =
                Quaternion.LookRotation(lookDirection, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                wantedRotation,
                rotationBlend
            );
        }
    }

    private Vector3 ClampToRoom(Vector3 position)
    {
        if (roomBounds == null)
            return position;

        Bounds bounds = roomBounds.bounds;

        float minX = bounds.min.x + wallPadding;
        float maxX = bounds.max.x - wallPadding;

        float minZ = bounds.min.z + wallPadding;
        float maxZ = bounds.max.z - wallPadding;

        position.x = Mathf.Clamp(
            position.x,
            Mathf.Min(minX, maxX),
            Mathf.Max(minX, maxX)
        );

        position.z = Mathf.Clamp(
            position.z,
            Mathf.Min(minZ, maxZ),
            Mathf.Max(minZ, maxZ)
        );

        return position;
    }
}