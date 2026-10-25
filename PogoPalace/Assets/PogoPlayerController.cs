
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

    [Header("Air Control Input")]
    public InputActionReference airMovementAction;
    public bool requireButtonForAirControl = true;
    public bool showAirControlDebug = false;

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

    [Header("Center of Mass")]
    public bool overrideCenterOfMass = true;
    public Vector3 centerOfMassOffset =
        new Vector3(0f, -0.75f, 0f);

    [Header("Center of Mass Debug")]
    public bool showCenterOfMass = true;
    public bool showCenterOfMassOnlyWhenSelected = false;
    public float centerOfMassGizmoRadius = 0.15f;
    public Color centerOfMassGizmoColor = Color.magenta;
    public bool showCenterOfMassConnection = true;
    public bool showCenterOfMassVerticalAxis = false;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private Rigidbody rb;
    private PogoSpring pogoSpring;

    private Vector2 moveInput;
    private bool grounded;
    private bool airControlButtonHeld;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private Vector3 lastCenterOfMassOffset;
    private bool lastOverrideCenterOfMass;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        pogoSpring = GetComponent<PogoSpring>();

        ApplyCenterOfMass();

        rb.interpolation =
            RigidbodyInterpolation.Interpolate;

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void OnEnable()
    {
        moveAction?.action.Enable();
        resetAction?.action.Enable();
        rotateAction?.action.Enable();
        airMovementAction?.action.Enable();
    }

    private void OnDisable()
    {
        moveAction?.action.Disable();
        resetAction?.action.Disable();
        rotateAction?.action.Disable();
        airMovementAction?.action.Disable();

        airControlButtonHeld = false;
    }

    private void Update()
    {
        bool previousAirControlButtonHeld =
            airControlButtonHeld;

        airControlButtonHeld =
            airMovementAction != null &&
            airMovementAction.action != null &&
            airMovementAction.action.IsPressed();

        if (showAirControlDebug &&
            previousAirControlButtonHeld != airControlButtonHeld)
        {
            Debug.Log(
                airControlButtonHeld
                    ? "Air movement button pressed"
                    : "Air movement button released"
            );
        }

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
        UpdateCenterOfMass();

        grounded =
            pogoSpring != null &&
            pogoSpring.IsGrounded;

        ApplyTilt();
        ApplyUprightBalance();
        ApplyAirRotationDamping();
        ApplyGroundTraction();
        ApplyTiltAirMovement();
        ApplyCustomGravity();
        ApplyManualRotation();
    }

    // =====================================
    // CENTER OF MASS
    // =====================================

    private void ApplyCenterOfMass()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null)
            return;

        if (overrideCenterOfMass)
            rb.centerOfMass = centerOfMassOffset;
        else
            rb.ResetCenterOfMass();

        lastCenterOfMassOffset = centerOfMassOffset;
        lastOverrideCenterOfMass = overrideCenterOfMass;
    }

    private void UpdateCenterOfMass()
    {
        if (lastCenterOfMassOffset != centerOfMassOffset ||
            lastOverrideCenterOfMass != overrideCenterOfMass)
        {
            ApplyCenterOfMass();
        }
    }

    private void OnValidate()
    {
        centerOfMassGizmoRadius =
            Mathf.Max(0.01f, centerOfMassGizmoRadius);

        if (Application.isPlaying)
            ApplyCenterOfMass();
    }

    // =====================================
    // TILT MOVEMENT
    // =====================================

    private void ApplyTilt()
    {
        if (cameraTransform == null)
            return;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

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

    // =====================================
    // CUSTOM GRAVITY
    // =====================================

    private void ApplyCustomGravity()
    {
        if (grounded || !useCustomGravity)
            return;

        float verticalVelocity = rb.linearVelocity.y;
        float gravityMultiplier;

        if (Mathf.Abs(verticalVelocity) <
            apexVelocityThreshold)
        {
            gravityMultiplier = apexGravityMultiplier;
        }
        else if (verticalVelocity < 0f)
        {
            gravityMultiplier = fallingGravityMultiplier;
        }
        else
        {
            gravityMultiplier = risingGravityMultiplier;
        }

        Vector3 additionalGravity =
            Physics.gravity * (gravityMultiplier - 1f);

        rb.AddForce(
            additionalGravity,
            ForceMode.Acceleration
        );
    }

    // =====================================
    // TILT-BASED AIR MOVEMENT
    // =====================================

    private void ApplyTiltAirMovement()
    {
        if (!useTiltAirMovement || grounded)
            return;

        // NEW INPUT SYSTEM:
        // Only accelerate in the air while
        // the assigned button action is held.
        if (requireButtonForAirControl &&
            !airControlButtonHeld)
        {
            return;
        }

        Vector3 pogoDirection = transform.up;

        Vector3 horizontalLean = new Vector3(
            pogoDirection.x,
            0f,
            pogoDirection.z
        );

        float leanAmount = horizontalLean.magnitude;

        if (leanAmount < airMovementDeadzone)
            return;

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

        Vector3 acceleration =
            movementDirection *
            airMovementAcceleration *
            normalizedLean;

        Vector3 predictedVelocity =
            horizontalVelocity +
            acceleration * Time.fixedDeltaTime;

        if (predictedVelocity.magnitude >
            maxAirMovementSpeed)
        {
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

    // =====================================
    // UPRIGHT BALANCE
    // =====================================

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

    // =====================================
    // AIR ROTATION DAMPING
    // =====================================

    private void ApplyAirRotationDamping()
    {
        if (grounded)
            return;

        rb.AddTorque(
            -rb.angularVelocity * airRotationDamping,
            ForceMode.Acceleration
        );
    }

    // =====================================
    // GROUND TRACTION
    // =====================================

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

        inputDirection =
            Vector3.ClampMagnitude(inputDirection, 1f);

        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        Vector3 targetVelocity =
            inputDirection * maxGroundSpeed;

        float acceleration =
            inputDirection.sqrMagnitude > 0.01f
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

    // =====================================
    // MANUAL Y ROTATION
    // =====================================

    private void ApplyManualRotation()
    {
        if (!enableManualRotation || rotateAction == null)
            return;

        float rotationInput =
            rotateAction.action.ReadValue<float>();

        Vector3 rotationAxis = Vector3.up;

        float currentRotationSpeed = Vector3.Dot(
            rb.angularVelocity,
            rotationAxis
        );

        float targetRotationSpeed =
            rotationInput * maxRotationSpeed;

        float rotationAcceleration =
            (targetRotationSpeed - currentRotationSpeed) *
            rotationTorque;

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

    // =====================================
    // RESET
    // =====================================

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

        ApplyCenterOfMass();

        if (showDebugLogs)
            Debug.Log("Pogo Player Reset!");
    }

    // =====================================
    // CENTER OF MASS GIZMOS
    // =====================================

    private void OnDrawGizmos()
    {
        if (!showCenterOfMassOnlyWhenSelected)
            DrawCenterOfMassGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (showCenterOfMassOnlyWhenSelected)
            DrawCenterOfMassGizmos();
    }

    private void DrawCenterOfMassGizmos()
    {
        if (!showCenterOfMass)
            return;

        Rigidbody body = GetComponent<Rigidbody>();

        if (body == null)
            return;

        Vector3 worldCenter;

        if (Application.isPlaying)
        {
            worldCenter = body.worldCenterOfMass;
        }
        else if (overrideCenterOfMass)
        {
            worldCenter =
                transform.TransformPoint(centerOfMassOffset);
        }
        else
        {
            worldCenter = body.worldCenterOfMass;
        }

        Gizmos.color = centerOfMassGizmoColor;

        Gizmos.DrawSphere(
            worldCenter,
            centerOfMassGizmoRadius
        );

        Gizmos.color = Color.white;

        Gizmos.DrawWireSphere(
            worldCenter,
            centerOfMassGizmoRadius * 1.15f
        );

        if (showCenterOfMassConnection)
        {
            Gizmos.color = Color.yellow;

            Gizmos.DrawLine(
                transform.position,
                worldCenter
            );

            Gizmos.DrawWireSphere(
                transform.position,
                centerOfMassGizmoRadius * 0.4f
            );
        }

        if (showCenterOfMassVerticalAxis)
        {
            Gizmos.color = Color.green;

            Gizmos.DrawLine(
                worldCenter - Vector3.up,
                worldCenter + Vector3.up
            );
        }
    }
}
