
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PogoThrust : MonoBehaviour
{
    [Header("References")]
    public Rigidbody playerRigidbody;
    public PogoSpring pogoSpring;
    public Transform pogoTip;
    public Transform pogoUpper;
    public InputActionReference thrustAction;

    [Header("Thrust Settings")]
    public bool enableThrust = true;
    public float thrustSpeed = 22f;
    public float thrustDuration = 0.18f;
    public float thrustAcceleration = 65f;

    [Header("Direction")]
    public bool lockDirectionOnActivation = true;

    [Header("Air Restrictions")]
    public bool requireAirborne = true;
    public bool oneThrustPerJump = true;

    [Header("Cooldown")]
    public float thrustCooldown = 0.5f;

    [Header("Debug")]
    public bool showDebug = false;

    [SerializeField] private bool isThrusting;
    [SerializeField] private bool hasThrusted;
    [SerializeField] private Vector3 thrustDirection;

    private bool wasGrounded;
    private bool thrustQueued;
    private float thrustTimer;
    private float cooldownTimer;

    public bool IsThrusting => isThrusting;

    private void Awake()
    {
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();

        if (pogoSpring == null)
            pogoSpring = GetComponent<PogoSpring>();
    }

    private void OnEnable()
    {
        if (thrustAction != null)
        {
            thrustAction.action.Enable();
            thrustAction.action.performed += OnThrust;
        }
    }

    private void OnDisable()
    {
        if (thrustAction != null)
        {
            thrustAction.action.performed -= OnThrust;
            thrustAction.action.Disable();
        }

        isThrusting = false;
        thrustQueued = false;
    }

    private void OnThrust(InputAction.CallbackContext context)
    {
        thrustQueued = true;
    }

    private void FixedUpdate()
    {
        if (playerRigidbody == null || pogoSpring == null)
            return;

        bool grounded = pogoSpring.IsGrounded;

        if (grounded && !wasGrounded)
        {
            hasThrusted = false;
            StopThrust();
        }

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.fixedDeltaTime;

        if (thrustQueued)
        {
            thrustQueued = false;

            if (CanThrust(grounded))
                StartThrust();
        }

        if (isThrusting)
        {
            if (grounded)
            {
                StopThrust();
            }
            else
            {
                UpdateThrust();
            }
        }

        wasGrounded = grounded;
    }

    private bool CanThrust(bool grounded)
    {
        if (!enableThrust)
            return false;

        if (requireAirborne && grounded)
            return false;

        if (oneThrustPerJump && hasThrusted)
            return false;

        if (cooldownTimer > 0f || isThrusting)
            return false;

        if (pogoTip == null || pogoUpper == null)
            return false;

        return true;
    }

    private Vector3 GetThrustDirection()
    {
        // Direction from the upper pogo body to its tip.
        Vector3 direction =
            pogoTip.position - pogoUpper.position;

        if (direction.sqrMagnitude < 0.0001f)
            return -transform.up;

        return direction.normalized;
    }

    private void StartThrust()
    {
        thrustDirection = GetThrustDirection();

        isThrusting = true;
        hasThrusted = true;

        thrustTimer = thrustDuration;
        cooldownTimer = thrustCooldown;

        // Immediately launch along the pogo's direction.
        Vector3 velocity = playerRigidbody.linearVelocity;

        float existingSpeed =
            Vector3.Dot(velocity, thrustDirection);

        float addedSpeed =
            Mathf.Max(0f, thrustSpeed - existingSpeed);

        playerRigidbody.AddForce(
            thrustDirection * addedSpeed,
            ForceMode.VelocityChange
        );

        if (showDebug)
        {
            Debug.Log(
                $"Pogo Thrust STARTED. Direction: {thrustDirection}"
            );
        }
    }

    private void UpdateThrust()
    {
        thrustTimer -= Time.fixedDeltaTime;

        if (!lockDirectionOnActivation)
            thrustDirection = GetThrustDirection();

        playerRigidbody.AddForce(
            thrustDirection * thrustAcceleration,
            ForceMode.Acceleration
        );

        if (thrustTimer <= 0f)
            StopThrust();
    }

    private void StopThrust()
    {
        if (!isThrusting)
            return;

        isThrusting = false;
        thrustTimer = 0f;

        if (showDebug)
            Debug.Log("Pogo Thrust ENDED");
    }

    private void OnDrawGizmosSelected()
    {
        if (pogoTip == null || pogoUpper == null)
            return;

        Vector3 direction =
            (pogoTip.position - pogoUpper.position).normalized;

        Gizmos.color = Color.cyan;

        Gizmos.DrawLine(
            pogoUpper.position,
            pogoTip.position + direction * 2f
        );

        Gizmos.DrawSphere(
            pogoTip.position + direction * 2f,
            0.12f
        );
    }
}
