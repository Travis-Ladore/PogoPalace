
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PogoSpring : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference jumpAction;

    [Header("Physics References")]
    public Transform springOrigin;

    [Tooltip("A fixed, unanimated reference marking the physical tip of the pogo.")]
    public Transform pogoTip;

    [Header("Visual References")]
    public Transform lowerRodVisual;
    public Transform springVisual;

    [Header("Ground Detection")]
    public LayerMask groundMask;
    public float rayRadius = 0.12f;

    [Range(0f, 90f)]
    public float maxGroundAngle = 55f;

    public bool restrictSpringTilt = true;

    [Range(0f, 180f)]
    public float maxSpringTilt = 70f;

    [Header("Spring Dimensions")]
    [Tooltip("Used as the physical spring length when automatic tip measurement is disabled.")]
    public float restLength = 1.5f;

    public float springTravel = 0.5f;

    [Header("Spring Geometry")]
    [Tooltip("Measure physical spring length from SpringOrigin to PogoTip.")]
    public bool calculateRestLengthFromTip = false;

    [Header("Spring Preload")]
    [Tooltip("Extends the spring's resting position beyond the physical rod tip.")]
    [Min(0f)]
    public float springPreload = 0.15f;

    [Header("Spring Force")]
    public float springStrength = 1600f;
    public float springDamping = 65f;
    public float maxSpringForce = 10000f;

    [Header("Spring Direction")]
    [Range(0f, 1f)]
    public float springTiltInfluence = 0.15f;

    [Header("Manual Compression")]
    public bool allowRetraction = true;
    public float maxRetraction = 0.4f;
    public float retractSpeed = 1.5f;
    public float extendSpeed = 5f;

    [Header("Perfect Bounce")]
    public bool enablePerfectBounce = true;

    [Range(0f, 1f)]
    public float perfectCompressionThreshold = 0.8f;

    public float perfectBounceMultiplier = 1.6f;
    public float normalReleaseImpulse = 30f;
    public float perfectReleaseImpulse = 65f;

    [Header("Rebound Control")]
    public float compressionDamping = 12f;
    public float reboundDamping = 3f;
    public float landingCompressionMultiplier = 1f;
    public float maxReboundSpeed = 14f;
    public float reboundForceFadeStart = 9f;
    public float maxLandingSpeedForSpring = 22f;

    [Header("Visual Settings")]
    public float visualSmoothness = 15f;

    [Header("Bounce Audio")]
    public bool enableBounceSound = true;
    public float minimumBounceSpeed = 2f;
    public float minimumImpactSpeedForSound = 2f;
    public float bounceSoundCooldown = 0.12f;

    [Header("Debug")]
    public bool showDebug = false;

    public bool IsGrounded { get; private set; }
    public float CompressionPercent { get; private set; }
    public bool PerfectBounceTriggered { get; private set; }

    private Rigidbody rb;

    private bool jumpHeld;
    private bool releaseQueued;

    private float currentRetraction;
    private float currentLength;
    private float compression;
    private float visualCompression;

    private Vector3 lowerStartPosition;
    private Vector3 springStartScale;

    private RaycastHit groundHit;
    private Vector3 springDirection;

    // Bounce audio state
    private float lastBounceSoundTime = -100f;
    private float previousUpwardSpeed;
    private float strongestLandingSpeed;
    private bool bounceContactActive;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        currentLength = GetRestLength();

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
        CalculateSpringDirection();
        ApplySpringForce();
        ProcessRelease();
        CheckBounceSound();
        UpdateVisuals();
    }

    // ==========================================
    // SPRING LENGTH AND PRELOAD
    // ==========================================

    private float GetRestLength()
    {
        float physicalLength = restLength;

        if (calculateRestLengthFromTip &&
            springOrigin != null &&
            pogoTip != null)
        {
            Vector3 originToTip =
                pogoTip.position - springOrigin.position;

            physicalLength = Vector3.Dot(
                originToTip,
                -transform.up
            );
        }

        return Mathf.Max(
            0.1f,
            physicalLength + springPreload
        );
    }

    private void UpdateRetraction()
    {
        float targetRetraction =
            allowRetraction && jumpHeld
                ? maxRetraction
                : 0f;

        float speed =
            jumpHeld ? retractSpeed : extendSpeed;

        currentRetraction = Mathf.MoveTowards(
            currentRetraction,
            targetRetraction,
            speed * Time.fixedDeltaTime
        );

        currentLength = Mathf.Max(
            0.1f,
            GetRestLength() - currentRetraction
        );
    }

    // ==========================================
    // GROUND DETECTION
    // ==========================================

    private void CalculateCompression()
    {
        IsGrounded = false;
        compression = 0f;
        CompressionPercent = 0f;

        if (restrictSpringTilt)
        {
            float tilt = Vector3.Angle(
                transform.up,
                Vector3.up
            );

            if (tilt > maxSpringTilt)
                return;
        }

        Vector3 origin = springOrigin != null
            ? springOrigin.position
            : transform.position;

        Vector3 down = -transform.up;

        float castDistance =
            currentLength + springTravel;

        if (!Physics.SphereCast(
            origin,
            rayRadius,
            down,
            out groundHit,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            return;
        }

        // Reject walls and steep slopes.
        float surfaceAngle = Vector3.Angle(
            groundHit.normal,
            Vector3.up
        );

        if (surfaceAngle > maxGroundAngle)
            return;

        IsGrounded = true;

        float groundDistance =
            groundHit.distance + rayRadius;

        compression = Mathf.Clamp(
            currentLength - groundDistance,
            0f,
            springTravel
        );

        CompressionPercent = Mathf.Clamp01(
            compression /
            Mathf.Max(0.01f, springTravel)
        );
    }

    // ==========================================
    // SPRING FORCE
    // ==========================================

    private void CalculateSpringDirection()
    {
        springDirection = Vector3.Slerp(
            Vector3.up,
            transform.up,
            springTiltInfluence
        ).normalized;
    }

    private void ApplySpringForce()
    {
        if (!IsGrounded || compression <= 0f)
            return;

        Vector3 origin = springOrigin != null
            ? springOrigin.position
            : transform.position;

        float velocity = Vector3.Dot(
            rb.GetPointVelocity(origin),
            springDirection
        );

        bool compressing = velocity < 0f;

        float damping = compressing
            ? compressionDamping
            : reboundDamping;

        float limitedVelocity = Mathf.Max(
            velocity,
            -maxLandingSpeedForSpring
        );

        float force =
            compression * springStrength -
            limitedVelocity * damping;

        if (compressing)
            force *= landingCompressionMultiplier;

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
            springDirection * force,
            ForceMode.Force
        );
    }

    // ==========================================
    // MANUAL RELEASE / PERFECT BOUNCE
    // ==========================================

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
            springDirection
        );

        float availableSpeed = Mathf.Max(
            0f,
            maxReboundSpeed - upwardSpeed
        );

        impulse = Mathf.Min(
            impulse,
            availableSpeed * rb.mass
        );

        rb.AddForce(
            springDirection * impulse,
            ForceMode.Impulse
        );

        PerfectBounceTriggered = perfect;
    }

    // ==========================================
    // AUTOMATIC BOUNCE SOUND
    // ==========================================

    private void CheckBounceSound()
    {
        if (rb == null)
            return;

        float upwardSpeed = rb.linearVelocity.y;

        if (IsGrounded && compression > 0f)
        {
            bounceContactActive = true;

            if (upwardSpeed < 0f)
            {
                strongestLandingSpeed = Mathf.Max(
                    strongestLandingSpeed,
                    -upwardSpeed
                );
            }
        }

        bool rebounded =
            previousUpwardSpeed <= minimumBounceSpeed &&
            upwardSpeed > minimumBounceSpeed;

        bool validBounce =
            bounceContactActive &&
            strongestLandingSpeed >=
            minimumImpactSpeedForSound;

        if (enableBounceSound &&
            rebounded &&
            validBounce &&
            Time.time - lastBounceSoundTime >=
            bounceSoundCooldown)
        {
            SoundManager.Instance?.Play("Jump");

            lastBounceSoundTime = Time.time;

            if (showDebug)
            {
                Debug.Log(
                    $"Pogo Bounce! Impact: " +
                    $"{strongestLandingSpeed:F2}, " +
                    $"Rebound: {upwardSpeed:F2}"
                );
            }

            bounceContactActive = false;
            strongestLandingSpeed = 0f;
        }

        if (!IsGrounded && upwardSpeed <= 0f)
        {
            bounceContactActive = false;
            strongestLandingSpeed = 0f;
        }

        previousUpwardSpeed = upwardSpeed;
    }

    // ==========================================
    // SPRING VISUALS
    // ==========================================

    private void UpdateVisuals()
    {
        float totalCompression =
            currentRetraction + compression;

        visualCompression = Mathf.Lerp(
            visualCompression,
            totalCompression,
            1f - Mathf.Exp(
                -visualSmoothness *
                Time.fixedDeltaTime
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
            float percent = Mathf.Clamp01(
                visualCompression /
                Mathf.Max(
                    0.01f,
                    springTravel + maxRetraction
                )
            );

            Vector3 scale = springStartScale;

            scale.y *= Mathf.Lerp(
                1f,
                0.35f,
                percent
            );

            springVisual.localScale = scale;
        }
    }

    // ==========================================
    // RESET
    // ==========================================

    public void ResetSpring()
    {
        jumpHeld = false;
        releaseQueued = false;

        currentRetraction = 0f;
        currentLength = GetRestLength();
        compression = 0f;
        visualCompression = 0f;

        IsGrounded = false;
        CompressionPercent = 0f;
        PerfectBounceTriggered = false;

        lastBounceSoundTime = -100f;
        previousUpwardSpeed = 0f;
        strongestLandingSpeed = 0f;
        bounceContactActive = false;

        if (lowerRodVisual != null)
        {
            lowerRodVisual.localPosition =
                lowerStartPosition;
        }

        if (springVisual != null)
        {
            springVisual.localScale =
                springStartScale;
        }
    }

    // ==========================================
    // DEBUG GIZMOS
    // ==========================================

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = springOrigin != null
            ? springOrigin.position
            : transform.position;

        Vector3 down = -transform.up;

        float physicalLength = restLength;

        if (calculateRestLengthFromTip &&
            springOrigin != null &&
            pogoTip != null)
        {
            physicalLength = Vector3.Dot(
                pogoTip.position - springOrigin.position,
                down
            );
        }

        physicalLength = Mathf.Max(0.1f, physicalLength);

        float restingLength = Application.isPlaying
            ? currentLength
            : GetRestLength();

        // Blue: physical tip position
        Gizmos.color = Color.blue;

        Gizmos.DrawWireSphere(
            origin + down * physicalLength,
            rayRadius
        );

        // Red: spring resting position including preload
        Gizmos.color = Color.red;

        Vector3 restPosition =
            origin + down * restingLength;

        Gizmos.DrawWireSphere(
            restPosition,
            rayRadius
        );

        Gizmos.DrawLine(origin, restPosition);

        // Yellow: maximum detection range
        Gizmos.color = Color.yellow;

        Vector3 maxPosition =
            origin + down * (restingLength + springTravel);

        Gizmos.DrawWireSphere(
            maxPosition,
            rayRadius
        );

        // Green: valid ground contact
        if (Application.isPlaying && IsGrounded)
        {
            Gizmos.color = Color.green;

            Gizmos.DrawWireSphere(
                groundHit.point,
                0.1f
            );
        }

        // Cyan: applied spring direction
        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;

            Gizmos.DrawRay(
                origin,
                springDirection * 2f
            );
        }
    }
}
