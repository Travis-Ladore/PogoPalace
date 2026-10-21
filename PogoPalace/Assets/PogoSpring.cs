
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PogoSpring : MonoBehaviour
{
    [Header("References")]
    public InputActionReference jumpAction;
    public LayerMask groundMask;
    public Transform lowerRodVisual;
    public Transform springVisual;

    [Header("Spring Geometry")]
    public float restLength = 1.5f;
    public float springTravel = 0.5f;
    public float rayRadius = 0.12f;

    [Header("Spring Physics")]
    public float springStrength = 1600f;
    public float springDamping = 65f;
    public float maxSpringForce = 10000f;

    [Header("Manual Retraction")]
    public bool allowRetraction = true;
    public float maxRetraction = 0.4f;
    public float retractSpeed = 1.5f;
    public float extendSpeed = 5f;

    [Header("Landing Response")]
    public float compressionDamping = 12f;
    public float reboundDamping = 3f;
    public float landingCompressionMultiplier = 1f;

    [Header("Bounce Stabilization")]
    public float maxReboundSpeed = 14f;
    public float reboundForceFadeStart = 9f;
    public float maxLandingSpeedForSpring = 22f;

    [Header("Bounce Timing")]
    public bool enablePerfectBounce = true;

    [Range(0f, 1f)]
    public float perfectCompressionThreshold = 0.8f;

    public float perfectBounceMultiplier = 1.6f;
    public float normalReleaseImpulse = 30f;
    public float perfectReleaseImpulse = 65f;

    [Header("Visuals")]
    public float visualSmoothness = 15f;

    [Header("Debug")]
    public bool showDebug = false;

    public bool IsGrounded { get; private set; }
    public float CompressionPercent { get; private set; }
    public bool PerfectBounceTriggered { get; private set; }
    private bool bounceSoundPlayed = false;

    private Rigidbody rb;

    private bool jumpHeld;
    private bool releaseQueued;
    private float currentRetraction;
    private float currentLength;
    private float compression;
    private float visualCompression;

    private Vector3 lowerStartPosition;
    private Vector3 springStartScale;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        currentLength = restLength;

        if (lowerRodVisual != null)
            lowerStartPosition = lowerRodVisual.localPosition;

        if (springVisual != null)
            springStartScale = springVisual.localScale;
    }

    private void OnEnable()
    {
        jumpAction?.action.Enable();
    }

    private void OnDisable()
    {
        jumpAction?.action.Disable();
    }

    private void Update()
    {
        if (jumpAction == null)
            return;

        bool held = jumpAction.action.IsPressed();

        if (jumpHeld && !held)
            releaseQueued = true;

        jumpHeld = held;
    }

    private void FixedUpdate()
    {
        UpdateRetraction();
        CalculateCompression();
        ApplySpringForce();
        ProcessRelease();
        CheckBounceSound();
        UpdateVisuals();
    }

    private void UpdateRetraction()
    {
        float target = 0f;

        if (allowRetraction && jumpHeld)
            target = maxRetraction;

        float speed = jumpHeld ? retractSpeed : extendSpeed;

        currentRetraction = Mathf.MoveTowards(
            currentRetraction,
            target,
            speed * Time.fixedDeltaTime
        );

        currentLength = Mathf.Max(
            0.1f,
            restLength - currentRetraction
        );
    }

    private void CalculateCompression()
    {
        IsGrounded = false;
        compression = 0f;
        CompressionPercent = 0f;

        Vector3 direction = -transform.up;

        float castDistance = restLength + springTravel;

        if (Physics.SphereCast(
            transform.position,
            rayRadius,
            direction,
            out RaycastHit hit,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            IsGrounded = true;

            compression = Mathf.Clamp(
                currentLength - hit.distance,
                0f,
                springTravel
            );

            CompressionPercent = Mathf.Clamp01(
                compression / Mathf.Max(0.01f, springTravel)
            );
        }
    }



    private void ApplySpringForce()
    {
        if (!IsGrounded || compression <= 0f)
            return;

        Vector3 up = transform.up;

        float velocity = Vector3.Dot(
            rb.GetPointVelocity(transform.position),
            up
        );

        bool compressing = velocity < 0f;

        float damping = compressing
            ? compressionDamping
            : reboundDamping;

        // Limit extreme landing velocities used in damping.
        float limitedVelocity = Mathf.Max(
            velocity,
            -maxLandingSpeedForSpring
        );

        float force =
            compression * springStrength -
            limitedVelocity * damping;

        if (compressing)
            force *= landingCompressionMultiplier;

        // Gradually reduce spring force as rebound speed rises.
        if (velocity > reboundForceFadeStart)
        {
            float fade = Mathf.InverseLerp(
                reboundForceFadeStart,
                maxReboundSpeed,
                velocity
            );

            force *= 1f - fade;
        }

        force = Mathf.Clamp(
            force,
            0f,
            maxSpringForce
        );

        rb.AddForce(
            up * force,
            ForceMode.Force
        );

    }



    private void ProcessRelease()
    {
        PerfectBounceTriggered = false;

        if (!releaseQueued)
            return;

        releaseQueued = false;

        if (!IsGrounded || !allowRetraction)
            return;

        bool perfect =
            enablePerfectBounce &&
            CompressionPercent >= perfectCompressionThreshold;

        float impulse = perfect
            ? perfectReleaseImpulse
            : normalReleaseImpulse;

        if (perfect)
            impulse *= perfectBounceMultiplier;

        float upwardSpeed = Vector3.Dot(
            rb.linearVelocity,
            transform.up
        );

        float availableSpeed =
            Mathf.Max(0f, maxReboundSpeed - upwardSpeed);

        float maxAllowedImpulse = availableSpeed * rb.mass;

        impulse = Mathf.Min(impulse, maxAllowedImpulse);

        rb.AddForce(
            transform.up * impulse,
            ForceMode.Impulse
        );
        

        PerfectBounceTriggered = perfect;

        if (showDebug)
        {
            Debug.Log(perfect
                ? "PERFECT BOUNCE!"
                : "Normal Bounce");
        }
    }

    private void UpdateVisuals()
    {
        float totalCompression =
            currentRetraction + compression;

        visualCompression = Mathf.Lerp(
            visualCompression,
            totalCompression,
            1f - Mathf.Exp(
                -visualSmoothness * Time.fixedDeltaTime
            )
        );

        if (lowerRodVisual != null)
        {
            lowerRodVisual.localPosition =
                lowerStartPosition +
                Vector3.up * visualCompression;
        }

        if (springVisual != null)
        {
            float maxCompression =
                springTravel + maxRetraction;

            float percent = Mathf.Clamp01(
                visualCompression /
                Mathf.Max(0.01f, maxCompression)
            );

            Vector3 scale = springStartScale;
            scale.y *= Mathf.Lerp(1f, 0.35f, percent);

            springVisual.localScale = scale;
        }
    }

    public void ResetSpring()
    {
        jumpHeld = false;
        releaseQueued = false;
        currentRetraction = 0f;
        currentLength = restLength;
        compression = 0f;
        visualCompression = 0f;

        IsGrounded = false;
        CompressionPercent = 0f;
        PerfectBounceTriggered = false;

        if (lowerRodVisual != null)
            lowerRodVisual.localPosition = lowerStartPosition;

        if (springVisual != null)
            springVisual.localScale = springStartScale;
    }
    private void CheckBounceSound()
    {
        // Reset when the pogo leaves the ground.
        if (!IsGrounded || compression <= 0f)
        {
            bounceSoundPlayed = false;
            return;
        }

        // Check whether the pogo is moving upward.
        float upwardVelocity = Vector3.Dot(
            rb.linearVelocity,
            transform.up
        );

        // Play once when the spring begins rebounding.
        if (upwardVelocity > 0.5f && !bounceSoundPlayed)
        {
            SoundManager.Instance?.Play("Jump");

            bounceSoundPlayed = true;
        }
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsGrounded ? Color.green : Color.red;

        Gizmos.DrawLine(
            transform.position,
            transform.position - transform.up * restLength
        );

        Gizmos.DrawWireSphere(
            transform.position - transform.up * restLength,
            rayRadius
        );
    }
}
