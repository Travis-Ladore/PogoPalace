
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PogoSpring))]
public class PogoPlayerController : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference moveAction;
    public InputActionReference resetAction;

    [Header("References")]
    public Transform cameraTransform;

    [Header("Tilt Movement")]
    public float tiltTorque = 35f;
    public float maxTiltAngle = 65f;

    [Header("Air Control")]
    public bool allowAirControl = true;
    public float airTorque = 45f;
    public float airRotationDamping = 0.5f;
    public bool allowFullAirRotation = true;

    [Header("Tilt-Based Air Movement")]
    public bool useTiltAirMovement = true;

    public float airMovementAcceleration = 10f;
    public float maxAirMovementSpeed = 12f;

    [Range(0f, 1f)]
    public float airMovementDeadzone = 0.1f;

    public float airMovementResponse = 1.5f;

    [Range(0f, 2f)]
    public float airControlStrength = 0.5f;

    [Header("Wobble Balance")]
    public float uprightStrength = 25f;
    public float uprightDamping = 5f;
    public bool balanceInAir = false;

    [Header("Manual Y Rotation")]
    public bool enableManualRotation = true;
    public InputActionReference rotateAction;

    public float rotationTorque = 20f;
    public float rotationDamping = 5f;
    public float maxRotationSpeed = 4f;

    [Header("Custom Jump Gravity")]
    public bool useCustomGravity = true;

    [Range(1f, 5f)]
    public float risingGravityMultiplier = 1.15f;

    [Range(1f, 6f)]
    public float fallingGravityMultiplier = 2.2f;

    [Range(1f, 5f)]
    public float apexGravityMultiplier = 1.6f;

    public float apexVelocityThreshold = 2f;

    [Header("Ground Traction")]
    public bool useGroundTraction = true;

    public float groundGrip = 12f;
    public float groundAcceleration = 8f;
    public float maxGroundSpeed = 7f;

    public float brakingStrength = 18f;


    public Vector3 centerOfMassOffset =
        new Vector3(0f, -0.75f, 0f);

    [Header("Debug")]
    public bool showDebugLogs = false;

    private Rigidbody rb;
    private PogoSpring pogoSpring;

    private Vector2 moveInput;
    private bool grounded;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        pogoSpring = GetComponent<PogoSpring>();

        rb.centerOfMass = centerOfMassOffset;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void OnEnable()
    {
        moveAction?.action.Enable();
        resetAction?.action.Enable();
        rotateAction?.action.Enable();
    }

    private void OnDisable()
    {
        moveAction?.action.Disable();
        resetAction?.action.Disable();
        rotateAction?.action.Disable();
    }

    private void Update()
    {
        if (moveAction != null)
        {
            moveInput =
                moveAction.action.ReadValue<Vector2>();
        }

        if (resetAction != null &&
            resetAction.action.WasPressedThisFrame())
        {
            ResetPlayer();
        }
    }

    private void FixedUpdate()
    {
        grounded = pogoSpring != null &&
                   pogoSpring.IsGrounded;

        ApplyTilt();
        ApplyUprightBalance();
        ApplyAirRotationDamping();
        ApplyGroundTraction();
        ApplyTiltAirMovement();
        ApplyCustomGravity();
        ApplyManualRotation();

    }


    private void ApplyTilt()
    {
        if (cameraTransform == null)
            return;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        // Keep input relative to the camera's horizontal direction.
        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 desiredDirection =
            forward * moveInput.y +
            right * moveInput.x;

        if (desiredDirection.sqrMagnitude < 0.01f)
            return;

        desiredDirection =
            Vector3.ClampMagnitude(desiredDirection, 1f);

        Vector3 torqueAxis = Vector3.Cross(
            Vector3.up,
            desiredDirection
        );

        if (grounded)
        {
            float tiltAngle = Vector3.Angle(
                transform.up,
                Vector3.up
            );

            if (tiltAngle < maxTiltAngle)
            {
                rb.AddTorque(
                    torqueAxis * tiltTorque,
                    ForceMode.Acceleration
                );
            }
        }
        else if (allowAirControl)
        {
            if (allowFullAirRotation)
            {
                // No tilt limit while airborne.
                rb.AddTorque(
                    torqueAxis * airTorque,
                    ForceMode.Acceleration
                );
            }
            else
            {
                float tiltAngle = Vector3.Angle(
                    transform.up,
                    Vector3.up
                );

                if (tiltAngle < maxTiltAngle)
                {
                    rb.AddTorque(
                        torqueAxis * airTorque,
                        ForceMode.Acceleration
                    );
                }
            }
        }
    }


    private void ApplyCustomGravity()
    {
        if (grounded)
            return;
        if (!useCustomGravity)
            return;

        // World-space vertical speed, independent of pogo rotation.
        float verticalVelocity = rb.linearVelocity.y;

        float gravityMultiplier;

        if (Mathf.Abs(verticalVelocity) < apexVelocityThreshold
            && !grounded)
        {
            // Reduce floating near the top of the jump.
            gravityMultiplier = apexGravityMultiplier;
        }
        else if (verticalVelocity < 0f)
        {
            // Stronger gravity when falling.
            gravityMultiplier = fallingGravityMultiplier;
        }
        else
        {
            // Normal-ish gravity while rising.
            gravityMultiplier = risingGravityMultiplier;
        }

        // Rigidbody already receives normal gravity when
        // Use Gravity is enabled, so add only the extra amount.
        Vector3 additionalGravity =
            Physics.gravity * (gravityMultiplier - 1f);

        rb.AddForce(
            additionalGravity,
            ForceMode.Acceleration
        );
    }



    private void ApplyTiltAirMovement()
    {
        if (!useTiltAirMovement || grounded)
            return;

        // The pogo's upward axis determines movement direction.
        Vector3 pogoDirection = transform.up;

        // Only use horizontal lean.
        Vector3 horizontalLean = new Vector3(
            pogoDirection.x,
            0f,
            pogoDirection.z
        );

        float leanAmount = horizontalLean.magnitude;

        // Ignore very small accidental tilts.
        if (leanAmount < airMovementDeadzone)
            return;

        // Normalize the lean strength above the deadzone.
        float normalizedLean = Mathf.InverseLerp(
            airMovementDeadzone,
            1f,
            leanAmount
        );

        normalizedLean = Mathf.Pow(
            normalizedLean,
            airMovementResponse
        );

        Vector3 movementDirection =
            horizontalLean.normalized;

        Vector3 horizontalVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z
        );

        // Desired acceleration based on the player's lean.
        Vector3 acceleration =
            movementDirection *
            airMovementAcceleration *
            normalizedLean;

        // Only limit acceleration that would increase speed
        // beyond the configured maximum.
        Vector3 predictedVelocity =
            horizontalVelocity +
            acceleration * Time.fixedDeltaTime;

        if (predictedVelocity.magnitude > maxAirMovementSpeed)
        {
            // Allow acceleration that reduces existing speed,
            // but prevent further speed buildup.
            if (Vector3.Dot(
                acceleration,
                horizontalVelocity
            ) > 0f)
            {
                acceleration = Vector3.zero;
            }
        }

        rb.AddForce(
            acceleration,
            ForceMode.Acceleration
        );
    }



    private void ApplyUprightBalance()
    {
        if (!grounded && !balanceInAir)
            return;

        Vector3 currentUp = transform.up;
        Vector3 targetUp = Vector3.up;

        Vector3 rotationAxis = Vector3.Cross(
            currentUp,
            targetUp
        );

        float tiltAngle = Vector3.Angle(
            currentUp,
            targetUp
        );

        Vector3 restoringTorque =
            rotationAxis *
            (tiltAngle * Mathf.Deg2Rad * uprightStrength);

        Vector3 dampingTorque =
            -rb.angularVelocity * uprightDamping;

        rb.AddTorque(
            restoringTorque + dampingTorque,
            ForceMode.Acceleration
        );
    }

    private void ApplyAirRotationDamping()
    {
        if (grounded)
            return;

        rb.AddTorque(
            -rb.angularVelocity * airRotationDamping,
            ForceMode.Acceleration
        );
    }


    private void ApplyGroundTraction()
    {
        if (!useGroundTraction || !grounded)
            return;

        if (cameraTransform == null)
            return;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 inputDirection =
            forward * moveInput.y +
            right * moveInput.x;

        inputDirection = Vector3.ClampMagnitude(
            inputDirection, 1f
        );

        Vector3 velocity = rb.linearVelocity;

        // Only modify horizontal velocity.
        Vector3 horizontalVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        Vector3 targetVelocity =
            inputDirection * maxGroundSpeed;

        // Different response when moving vs releasing input.
        float acceleration = inputDirection.sqrMagnitude > 0.01f
            ? groundAcceleration
            : brakingStrength;

        Vector3 velocityDifference =
            targetVelocity - horizontalVelocity;

        Vector3 desiredAcceleration =
            velocityDifference * groundGrip;

        desiredAcceleration = Vector3.ClampMagnitude(
            desiredAcceleration,
            acceleration
        );

        rb.AddForce(
            desiredAcceleration,
            ForceMode.Acceleration
        );
    }



    private void ApplyManualRotation()
    {
        if (!enableManualRotation || rotateAction == null)
            return;

        float rotationInput =
            rotateAction.action.ReadValue<float>();

        // Use the world's vertical axis so the player
        // rotates horizontally even when leaning.
        Vector3 rotationAxis = Vector3.up;

        float currentRotationSpeed = Vector3.Dot(
            rb.angularVelocity,
            rotationAxis
        );

        // Target angular velocity in radians per second.
        float targetRotationSpeed =
            rotationInput * maxRotationSpeed;

        // Accelerate toward the desired rotation speed.
        float rotationAcceleration =
            (targetRotationSpeed - currentRotationSpeed) *
            rotationTorque;

        // Additional damping when no key is held.
        if (Mathf.Abs(rotationInput) < 0.01f)
        {
            rotationAcceleration =
                -currentRotationSpeed * rotationDamping;
        }

        rb.AddTorque(
            rotationAxis * rotationAcceleration,
            ForceMode.Acceleration
        );
    }




    public void ResetPlayer()
    {
        rb.position = spawnPosition;
        rb.rotation = spawnRotation;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        moveInput = Vector2.zero;
        grounded = false;

        if (pogoSpring != null)
            pogoSpring.ResetSpring();

        if (showDebugLogs)
            Debug.Log("Pogo Player Reset!");
    }
}
