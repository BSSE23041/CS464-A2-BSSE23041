using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpHeight = 1.8f; // metres
    [SerializeField] private float turnSpeed = 12f;   // how fast the player faces its travel direction

    [Header("Ground Detection")]
    [SerializeField] private Transform feet;
    [SerializeField] private float checkRadius = 0.2f;
    [SerializeField] private LayerMask groundMask;

    [Header("Part B - Jump Fairness")]
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBuffer = 0.12f;
    [SerializeField] private float fallMultiplier = 2.2f;

    [Header("Part B - Optional Short Hop (off = full-height jumps)")]
    [SerializeField] private bool enableShortHop = false;
    [SerializeField] private float shortHopGravityMultiplier = 2f;

    [Header("Part B - Landing Squash")]
    [SerializeField] private Transform body;
    [SerializeField] private float squashDuration = 0.35f;
    [SerializeField] private float squashY = 0.7f;

    private Rigidbody rb;
    private Transform cam;
    private Collider col;

    // Built in Update, applied in FixedUpdate.
    private Vector3 move;
    private bool jumpHeld;

    private bool isGrounded;
    private bool wasGrounded;
    private bool jumpQueued;

    private bool jumpLockedUntilLanding;
    private bool actuallyLeftGround;

    private float lastGroundedTime = -1f;
    private float lastPressedTime = -1f;
    private float airStartTime = -1f;
    private float jumpStartTime = -1f;

    private Coroutine squashCoroutine;
    private Vector3 originalBodyScale = Vector3.one;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Set in code so it works even if the prefab inspector is wrong.
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        col = GetComponent<Collider>();

        // Sensible default: if the mask was left on Nothing, use the Ground layer.
        if (groundMask.value == 0)
        {
            groundMask = LayerMask.GetMask("Ground");

            if (groundMask.value == 0)
                Debug.LogWarning("PlayerController: groundMask is empty and no 'Ground' layer exists. The player will never be grounded.", this);
        }

        if (Camera.main != null)
            cam = Camera.main.transform;

        if (body != null)
            originalBodyScale = body.localScale;
    }

    private void Update()
    {
        // The camera may spawn after the player, so keep trying until found.
        if (cam == null && Camera.main != null)
            cam = Camera.main.transform;

        // ---------- Input (all read in Update) ----------
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        jumpHeld = Input.GetKey(KeyCode.Space);

        // Camera-relative movement: W walks away from the camera.
        Vector3 forward = cam != null ? cam.forward : Vector3.forward;
        Vector3 right = cam != null ? cam.right : Vector3.right;

        forward.y = 0f;
        forward.Normalize();

        right.y = 0f;
        right.Normalize();

        // Clamp so diagonals are not faster than straight lines.
        move = Vector3.ClampMagnitude(forward * v + right * h, 1f);

        // ---------- Ground ----------
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
        if (isGrounded && !wasGrounded && verticalVelocity <= 0f)
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

                if (airStartTime >= 0f && airTime > 0.1f)
                    PlayLandingSquash();
            }
        }

        // Safety: a jump only leaves the ground sphere for a moment, so being
        // grounded again more than 0.25 s after takeoff means the player has
        // landed (on a ramp, a rising platform, or a ceiling blocked the jump).
        // Release the lock so the next press can always jump.
        if (jumpLockedUntilLanding &&
            !jumpQueued &&
            isGrounded &&
            Time.time - jumpStartTime > 0.25f)
        {
            jumpLockedUntilLanding = false;
            actuallyLeftGround = false;
        }

        // Refresh the ground timestamp only when eligible: not rising,
        // not queued, not waiting for a real landing.
        if (isGrounded &&
            verticalVelocity <= 0f &&
            !jumpQueued &&
            !jumpLockedUntilLanding)
        {
            lastGroundedTime = Time.time;
        }

        // Record jump button press.
        if (Input.GetKeyDown(KeyCode.Space))
            lastPressedTime = Time.time;

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

            // Use up both windows.
            lastGroundedTime = -1f;
            lastPressedTime = -1f;

            // Prevent a second jump until a real landing.
            jumpLockedUntilLanding = true;
            actuallyLeftGround = false;
            airStartTime = Time.time;
            jumpStartTime = Time.time;
        }

        wasGrounded = isGrounded;
    }

    private void FixedUpdate()
    {
        // Horizontal velocity from the move vector built in Update. Keep y.
        Vector3 currentVelocity = GetVelocity();

        SetVelocity(new Vector3(
            move.x * speed,
            currentVelocity.y,
            move.z * speed
        ));

        // Face the direction of travel.
        if (move.sqrMagnitude > 0.01f)
        {
            Quaternion wanted = Quaternion.LookRotation(move, Vector3.up);

            rb.MoveRotation(Quaternion.Slerp(
                rb.rotation,
                wanted,
                1f - Mathf.Exp(-turnSpeed * Time.fixedDeltaTime)
            ));
        }

        // Execute queued jump, once.
        if (jumpQueued)
        {
            jumpQueued = false;

            // Zero vertical velocity first so a jump taken while falling is full height.
            Vector3 velocity = GetVelocity();
            velocity.y = 0f;
            SetVelocity(velocity);

            // v0 = sqrt(2 g h); Impulse divides by mass, so apply m * v0.
            float g = -Physics.gravity.y;
            float v0 = Mathf.Sqrt(2f * g * jumpHeight);

            rb.AddForce(
                Vector3.up * rb.mass * v0,
                ForceMode.Impulse
            );

            lastGroundedTime = -1f;
            lastPressedTime = -1f;
        }

        float verticalVelocity = GetVelocity().y;

        // Heavier falling: same apex, shorter fall.
        if (verticalVelocity < 0f)
        {
            rb.AddForce(
                Physics.gravity * (fallMultiplier - 1f),
                ForceMode.Acceleration
            );
        }

        // Optional short hop when Space is released early.
        if (enableShortHop &&
            verticalVelocity > 0f &&
            !jumpHeld)
        {
            rb.AddForce(
                Physics.gravity * (shortHopGravityMultiplier - 1f),
                ForceMode.Acceleration
            );
        }
    }

    // Feet transform if assigned; otherwise the bottom of the collider.
    private Vector3 GetGroundCheckPosition()
    {
        if (feet != null)
            return feet.position;

        if (col == null)
            col = GetComponent<Collider>();

        if (col != null)
        {
            Bounds b = col.bounds;
            return new Vector3(b.center.x, b.min.y, b.center.z);
        }

        return transform.position;
    }

    private bool GroundCheck()
    {
        isGrounded = Physics.CheckSphere(
            GetGroundCheckPosition(),
            checkRadius,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        return isGrounded;
    }

    // Landing squash: once per real landing.
    private void PlayLandingSquash()
    {
        if (body == null)
            return;

        if (squashCoroutine != null)
            StopCoroutine(squashCoroutine);

        // Start from rest so an interrupted squash never leaves a stray scale.
        body.localScale = originalBodyScale;

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

        // Phase 1: compress on impact.
        float compressionDuration = duration * 0.25f;

        for (float elapsed = 0f;
             elapsed < compressionDuration;
             elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / compressionDuration);

            body.localScale = Vector3.Lerp(
                originalBodyScale,
                squashedScale,
                t
            );

            yield return null;
        }

        body.localScale = squashedScale;

        // Phase 2: spring back with Ease.OutBack.
        float recoveryDuration = duration * 0.75f;

        for (float elapsed = 0f;
             elapsed < recoveryDuration;
             elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / recoveryDuration);

            float p = Ease.OutBack(t);

            // LerpUnclamped keeps the overshoot.
            body.localScale = Vector3.LerpUnclamped(
                squashedScale,
                originalBodyScale,
                p
            );

            yield return null;
        }

        // Exactly at rest.
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
        Gizmos.color = isGrounded ? Color.green : Color.red;

        Gizmos.DrawWireSphere(GetGroundCheckPosition(), checkRadius);
    }
}