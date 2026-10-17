
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Camera Position")]
    [SerializeField]
    private Vector3 offset =
        new Vector3(0f, 4f, -7f);

    [Header("Position Smoothing")]
    [SerializeField] private float sharpness = 5f;

    [Header("Look Ahead")]
    [SerializeField] private float lookAhead = 0f;

    [Header("Rotation Smoothing")]
    [SerializeField] private float rotationSharpness = 2f;

    [Header("Camera Boundaries")]
    [SerializeField] private Collider roomBounds;
    [SerializeField] private float wallPadding = 0.5f;

    private Rigidbody targetRb;
    private Vector3 smoothedAimPoint;
    private Quaternion smoothedPlayerRotation;

    private void Awake()
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
            smoothedPlayerRotation = target.rotation;

            Vector3 startPosition =
                target.position + target.rotation * offset;

            transform.position = ClampToRoom(startPosition);
            transform.LookAt(target.position);
        }
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        float positionBlend =
            1f - Mathf.Exp(-sharpness * Time.deltaTime);

        float rotationBlend =
            1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);

        // Smoothly follow Jerry's turning.
        smoothedPlayerRotation = Quaternion.Slerp(
            smoothedPlayerRotation,
            target.rotation,
            rotationBlend
        );

        // Get horizontal movement velocity.
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

        // Predict where Jerry is moving.
        Vector3 aimPoint =
            target.position + flatVelocity * lookAhead;

        smoothedAimPoint = Vector3.Lerp(
            smoothedAimPoint,
            aimPoint,
            positionBlend
        );

        // Keep the camera behind Jerry.
        Vector3 rotatedOffset =
            smoothedPlayerRotation * offset;

        Vector3 wantedPosition =
            smoothedAimPoint + rotatedOffset;

        // Smooth camera movement.
        Vector3 smoothPosition = Vector3.Lerp(
            transform.position,
            wantedPosition,
            positionBlend
        );

        // NEW: Keep camera within room boundaries.
        transform.position = ClampToRoom(smoothPosition);

        // Smoothly look toward Jerry.
        Vector3 lookDirection =
            smoothedAimPoint - transform.position;

        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion wantedRotation =
                Quaternion.LookRotation(
                    lookDirection,
                    Vector3.up
                );

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
