
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpHeight = 1.8f;

    [Header("Player Rotation")]
    [SerializeField] private float rotationSpeed = 100f;

    [Header("Ground Detection")]
    [SerializeField] private Transform feet;
    [SerializeField] private float checkRadius = 0.2f;
    [SerializeField] private LayerMask groundMask;

    [Header("Part B - Jump Fairness")]
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBuffer = 0.12f;
    [SerializeField] private float fallMultiplier = 2.2f;

    [Header("Part B - Optional Short Hop")]
    [SerializeField] private bool enableShortHop = true;
    [SerializeField] private float shortHopGravityMultiplier = 2f;

    [Header("Part B - Landing Squash")]
    [SerializeField] private Transform body;
    [SerializeField] private float squashDuration = 0.35f;
    [SerializeField] private float squashY = 0.2f;

    private Rigidbody rb;
    private Vector3 moveInput;

    private float turnInput;
    private float forwardInput;
    private bool jumpHeld;

    private bool isGrounded;
    private bool wasGrounded;
    private bool jumpQueued;

    private bool jumpLockedUntilLanding;
    private bool actuallyLeftGround;

    private float lastGroundedTime = -1f;
    private float lastPressedTime = -1f;
    private float airStartTime;

    private Coroutine squashCoroutine;
    private Vector3 originalBodyScale;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (body != null)
            originalBodyScale = body.localScale;
    }

    private void Update()
    {
        // W/S: Move forward or backward.
        // A/D: Rotate left or right without sideways movement.
        forwardInput = Input.GetAxisRaw("Vertical");
        turnInput = Input.GetAxisRaw("Horizontal");

        // Cache jump-held state for FixedUpdate.
        jumpHeld = Input.GetKey(KeyCode.Space);

        // Check whether Jerry is touching the ground.
        GroundCheck();

        float verticalVelocity = GetVelocity().y;

        // Track when the player leaves the ground.
        if (!isGrounded)
        {
            if (wasGrounded && !jumpLockedUntilLanding)
                airStartTime = Time.time;

            if (jumpLockedUntilLanding)
                actuallyLeftGround = true;
        }

        // Detect a real landing.
        if (isGrounded && !wasGrounded &&
            verticalVelocity <= 0f)
        {
            if (jumpLockedUntilLanding && actuallyLeftGround)
            {
                float airTime = Time.time - airStartTime;

                if (airTime > 0.1f)
                    PlayLandingSquash();

                jumpLockedUntilLanding = false;
                actuallyLeftGround = false;
            }
            else if (!jumpLockedUntilLanding)
            {
                float airTime = Time.time - airStartTime;

                if (airTime > 0.1f && airStartTime > 0f)
                    PlayLandingSquash();
            }
        }

        // Refresh ground timestamp only when eligible.
        // Do not reopen the jump window while rising,
        // queued, or waiting for a real landing.
        if (isGrounded &&
            verticalVelocity <= 0f &&
            !jumpQueued &&
            !jumpLockedUntilLanding)
        {
            lastGroundedTime = Time.time;
        }

        // Record jump button press.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            lastPressedTime = Time.time;
            Debug.Log("Jump Pressed!");
        }

        // Coyote time and jump buffering.
        bool canJump =
            lastGroundedTime >= 0f &&
            Time.time - lastGroundedTime <= coyoteTime;

        bool wantJump =
            lastPressedTime >= 0f &&
            Time.time - lastPressedTime <= jumpBuffer;

        if (canJump && wantJump &&
            !jumpQueued &&
            !jumpLockedUntilLanding)
        {
            jumpQueued = true;

            // Consume both timestamps.
            lastGroundedTime = -1f;
            lastPressedTime = -1f;

            // Prevent a second jump during takeoff.
            jumpLockedUntilLanding = true;
            actuallyLeftGround = false;
            airStartTime = Time.time;

            Debug.Log("Jump Queued!");
        }

        wasGrounded = isGrounded;
    }

    private void FixedUpdate()
    {
        // Rotate the Rigidbody around the vertical Y-axis.
        // Positive D turns right; negative A turns left.
        // Rotate ONLY while A or D is pressed.
        Quaternion newRotation = rb.rotation;

        if (turnInput != 0f)
        {
            float turnAmount =
                turnInput * rotationSpeed * Time.fixedDeltaTime;

            newRotation = rb.rotation *
                Quaternion.Euler(0f, turnAmount, 0f);

            rb.MoveRotation(newRotation);
        }

        // Stop any residual physics rotation immediately.
        rb.angularVelocity = Vector3.zero;

        // Move only along the direction Jerry faces.
        // A/D alone do not produce horizontal movement.
        moveInput = newRotation * Vector3.forward * forwardInput;

        Vector3 currentVelocity = GetVelocity();

        SetVelocity(new Vector3(
            moveInput.x * speed,
            currentVelocity.y,
            moveInput.z * speed
        ));

        // Execute queued jump.
        if (jumpQueued)
        {
            jumpQueued = false;

            // Reset vertical velocity before jumping.
            Vector3 velocity = GetVelocity();
            velocity.y = 0f;
            SetVelocity(velocity);

            // Calculate impulse from jump height.
            float g = -Physics.gravity.y;
            float v0 = Mathf.Sqrt(2f * g * jumpHeight);

            rb.AddForce(
                Vector3.up * rb.mass * v0,
                ForceMode.Impulse
            );

            lastGroundedTime = -1f;
            lastPressedTime = -1f;

            Debug.Log("Jump Executed!");
        }

        float verticalVelocity = GetVelocity().y;

        // Heavier falling.
        if (verticalVelocity < 0f)
        {
            rb.AddForce(
                Physics.gravity * (fallMultiplier - 1f),
                ForceMode.Acceleration
            );
        }

        // Short hop when Space is released early.
        if (enableShortHop &&
            verticalVelocity > 0f &&
            !jumpHeld)
        {
            rb.AddForce(
                Physics.gravity *
                (shortHopGravityMultiplier - 1f),
                ForceMode.Acceleration
            );
        }
    }

    private bool GroundCheck()
    {
        Vector3 groundCheckPos =
            feet != null ? feet.position : transform.position;

        isGrounded = Physics.CheckSphere(
            groundCheckPos,
            checkRadius,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        return isGrounded;
    }

    // Landing squash: trigger once per real landing.
    private void PlayLandingSquash()
    {
        if (body == null)
            return;

        if (squashCoroutine != null)
            StopCoroutine(squashCoroutine);

        squashCoroutine = StartCoroutine(Squash());
    }

    private IEnumerator Squash()
    {
        float safeY = Mathf.Clamp(squashY, 0.1f, 1f);

        // Preserve approximate volume.
        float horizontalScale = 1f / Mathf.Sqrt(safeY);

        Vector3 squashedScale = new Vector3(
            originalBodyScale.x * horizontalScale,
            originalBodyScale.y * safeY,
            originalBodyScale.z * horizontalScale
        );

        float duration = Mathf.Max(0.01f, squashDuration);

        // Phase 1: Compress on impact.
        float compressionDuration = duration * 0.25f;

        for (float elapsed = 0f;
             elapsed < compressionDuration;
             elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(
                elapsed / compressionDuration
            );

            body.localScale = Vector3.Lerp(
                originalBodyScale,
                squashedScale,
                t
            );

            yield return null;
        }

        body.localScale = squashedScale;

        // Phase 2: Spring back with Ease.OutBack.
        float recoveryDuration = duration * 0.75f;

        for (float elapsed = 0f;
             elapsed < recoveryDuration;
             elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(
                elapsed / recoveryDuration
            );

            float p = Ease.OutBack(t);

            // Preserve overshoot.
            body.localScale = Vector3.LerpUnclamped(
                squashedScale,
                originalBodyScale,
                p
            );

            yield return null;
        }

        // Restore exact original scale.
        body.localScale = originalBodyScale;
        squashCoroutine = null;
    }

    private Vector3 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private void SetVelocity(Vector3 newVelocity)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = newVelocity;
#else
        rb.velocity = newVelocity;
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 groundCheckPos =
            feet != null ? feet.position : transform.position;

        Gizmos.color = isGrounded ? Color.green : Color.red;

        Gizmos.DrawWireSphere(
            groundCheckPos,
            checkRadius
        );
    }
}
