
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PogoGroundPound : MonoBehaviour
{
    [Header("References")]
    public Rigidbody playerRigidbody;
    public PogoSpring pogoSpring;
    public InputActionReference groundPoundAction;

    [Header("Ground Pound")]
    public bool enableGroundPound = true;
    public bool pressAgainToCancel = true;

    [Header("Height Requirement")]
    public bool requireMinimumHeight = true;
    [Min(0f)]
    public float minimumHeightAboveTakeoff = 3f;

    [Header("Falling")]
    public float downwardAcceleration = 45f;
    public float initialDownwardSpeed = 8f;
    public float maximumFallSpeed = 35f;

    [Header("Movement")]
    public bool restrictHorizontalMovement = false;

    [Range(0f, 1f)]
    public float horizontalVelocityMultiplier = 0.98f;

    [Header("Debug")]
    public bool showDebug = false;

    [SerializeField]
    private float takeoffHeight;

    [SerializeField]
    private float currentHeightAboveTakeoff;

    [SerializeField]
    private bool isGrounded;

    [SerializeField]
    private bool canGroundPound;

    public bool IsGroundPounding { get; private set; }

    private bool wasGrounded;
    private bool activationQueued;
    private bool hasTakenOff;

    private void Awake()
    {
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();

        if (pogoSpring == null)
            pogoSpring = GetComponent<PogoSpring>();

        takeoffHeight = transform.position.y;
    }

    private void OnEnable()
    {
        if (groundPoundAction != null)
        {
            groundPoundAction.action.Enable();
            groundPoundAction.action.performed += OnGroundPound;
        }
    }

    private void OnDisable()
    {
        if (groundPoundAction != null)
        {
            groundPoundAction.action.performed -= OnGroundPound;
            groundPoundAction.action.Disable();
        }

        IsGroundPounding = false;
        activationQueued = false;
    }

    private void OnGroundPound(
        InputAction.CallbackContext context)
    {
        if (enableGroundPound)
            activationQueued = true;
    }

    private void FixedUpdate()
    {
        if (playerRigidbody == null || pogoSpring == null)
            return;

        isGrounded = pogoSpring.IsGrounded;

        // Detect leaving the ground after a jump or bounce.
        if (wasGrounded && !isGrounded)
        {
            takeoffHeight = transform.position.y;
            hasTakenOff = true;

            if (showDebug)
            {
                Debug.Log(
                    $"Pogo takeoff recorded at Y={takeoffHeight:F2}"
                );
            }
        }

        // Detect landing.
        if (!wasGrounded && isGrounded)
        {
            hasTakenOff = false;

            if (IsGroundPounding)
                StopGroundPound();
        }

        currentHeightAboveTakeoff =
            transform.position.y - takeoffHeight;

        canGroundPound =
            enableGroundPound &&
            !isGrounded &&
            hasTakenOff &&
            (!requireMinimumHeight ||
             currentHeightAboveTakeoff >= minimumHeightAboveTakeoff);

        if (!enableGroundPound)
        {
            if (IsGroundPounding)
                StopGroundPound();

            activationQueued = false;
            wasGrounded = isGrounded;
            return;
        }

        if (activationQueued)
        {
            activationQueued = false;

            if (IsGroundPounding)
            {
                if (pressAgainToCancel)
                    StopGroundPound();
            }
            else if (canGroundPound)
            {
                StartGroundPound();
            }
            else if (showDebug)
            {
                Debug.Log(
                    $"Ground pound unavailable. " +
                    $"Height: {currentHeightAboveTakeoff:F2} / " +
                    $"{minimumHeightAboveTakeoff:F2}"
                );
            }
        }

        if (isGrounded && IsGroundPounding)
            StopGroundPound();

        if (IsGroundPounding)
            ApplyGroundPound();

        wasGrounded = isGrounded;
    }

    private void StartGroundPound()
    {
        IsGroundPounding = true;

        Vector3 velocity = playerRigidbody.linearVelocity;

        velocity.y = Mathf.Min(
            velocity.y,
            -initialDownwardSpeed
        );

        playerRigidbody.linearVelocity = velocity;

        if (showDebug)
            Debug.Log("Ground Pound STARTED");
    }

    private void StopGroundPound()
    {
        IsGroundPounding = false;

        if (showDebug)
            Debug.Log("Ground Pound STOPPED");
    }

    private void ApplyGroundPound()
    {
        Vector3 velocity = playerRigidbody.linearVelocity;

        playerRigidbody.AddForce(
            Vector3.down * downwardAcceleration,
            ForceMode.Acceleration
        );

        if (restrictHorizontalMovement)
        {
            velocity.x *= horizontalVelocityMultiplier;
            velocity.z *= horizontalVelocityMultiplier;
        }

        velocity.y = Mathf.Max(
            velocity.y,
            -maximumFallSpeed
        );

        playerRigidbody.linearVelocity = velocity;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            return;

        float requiredY =
            takeoffHeight + minimumHeightAboveTakeoff;

        Vector3 center = transform.position;
        center.y = requiredY;

        Gizmos.color = Color.yellow;

        Gizmos.DrawLine(
            center + Vector3.left * 2f,
            center + Vector3.right * 2f
        );

        Gizmos.color = canGroundPound
            ? Color.green
            : Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            0.3f
        );
    }
}
