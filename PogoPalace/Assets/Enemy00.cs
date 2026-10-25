
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyHealth))]
public class Enemy00 : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Rigidbody playerRigidbody;
    public Rigidbody rb;
    public EnemyHealth enemyHealth;

    [Header("Detection")]
    public float detectionRange = 30f;
    public float stoppingDistance = 2f;

    [Header("Ground Movement")]
    public float moveSpeed = 4f;
    public float acceleration = 15f;
    public float groundDrag = 5f;

    [Header("Jump Settings")]
    public bool enableJumping = true;
    public float jumpRange = 12f;
    public float minimumJumpDistance = 2f;
    public float jumpCooldown = 2f;
    public float jumpForce = 8f;
    public float horizontalJumpSpeed = 8f;

    [Header("Jump Prediction")]
    public bool enablePrediction = true;

    [Range(0f, 2f)]
    public float predictionStrength = 1f;

    public float maxPredictionTime = 1.5f;
    public float predictionRandomness = 1f;

    [Header("Air Control")]
    public bool allowAirControl = true;
    public float airAcceleration = 3f;
    public float maxAirSpeed = 10f;

    [Header("Ground Detection")]
    public LayerMask groundMask;
    public float groundCheckDistance = 0.25f;
    public float groundCheckRadius = 0.3f;
    public Transform groundCheck;

    [Header("Knockback Recovery")]
    public float knockbackRecoveryTime = 0.5f;

    [Header("Debug")]
    public bool showDebug = false;
    public bool drawPrediction = true;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isJumping;

    private float lastJumpTime = -100f;
    private float knockbackTimer;

    private Vector3 predictedTargetPosition;
    private Vector3 jumpDirection;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (enemyHealth == null)
            enemyHealth = GetComponent<EnemyHealth>();

        rb.interpolation =
            RigidbodyInterpolation.Interpolate;

        // Keep the enemy upright.
        rb.constraints |=
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        if (enemyHealth != null)
            enemyHealth.OnDamaged += OnDamaged;
    }

    private void Start()
    {
        FindPlayer();
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
            enemyHealth.OnDamaged -= OnDamaged;
    }

    private void FindPlayer()
    {
        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player != null && playerRigidbody == null)
        {
            playerRigidbody =
                player.GetComponent<Rigidbody>();
        }
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        if (enemyHealth != null && enemyHealth.IsDead)
            return;

        if (player == null)
        {
            FindPlayer();
            return;
        }

        CheckGrounded();

        // Allow physics knockback to affect the enemy
        // before it resumes chasing.
        if (knockbackTimer > 0f)
        {
            knockbackTimer = Mathf.Max(
                0f,
                knockbackTimer - Time.fixedDeltaTime
            );

            return;
        }

        float distanceToPlayer =
            Vector3.Distance(
                transform.position,
                player.position
            );

        if (distanceToPlayer > detectionRange)
            return;

        if (isGrounded)
        {
            HandleGroundMovement(distanceToPlayer);
            HandleJumping(distanceToPlayer);
        }
        else if (allowAirControl)
        {
            HandleAirMovement();
        }
    }

    // =====================================
    // GROUND DETECTION
    // =====================================

    private void CheckGrounded()
    {
        wasGrounded = isGrounded;

        Vector3 origin =
            groundCheck != null
                ? groundCheck.position
                : transform.position + Vector3.up * 0.1f;

        isGrounded = Physics.SphereCast(
            origin,
            groundCheckRadius,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (isGrounded && !wasGrounded)
        {
            isJumping = false;
        }
    }

    // =====================================
    // GROUND MOVEMENT
    // =====================================

    private void HandleGroundMovement(float distance)
    {
        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        Vector3 horizontalVelocity =
            new Vector3(
                rb.linearVelocity.x,
                0f,
                rb.linearVelocity.z
            );

        Vector3 targetVelocity =
            distance > stoppingDistance
                ? direction * moveSpeed
                : Vector3.zero;

        float currentAcceleration =
            distance > stoppingDistance
                ? acceleration
                : groundDrag;

        Vector3 velocityChange =
            targetVelocity - horizontalVelocity;

        Vector3 accelerationVector =
            Vector3.ClampMagnitude(
                velocityChange / Time.fixedDeltaTime,
                currentAcceleration
            );

        rb.AddForce(
            accelerationVector,
            ForceMode.Acceleration
        );
    }

    // =====================================
    // PREDICTIVE JUMPING
    // =====================================

    private void HandleJumping(float distance)
    {
        if (!enableJumping || isJumping)
            return;

        if (Time.time - lastJumpTime < jumpCooldown)
            return;

        if (distance > jumpRange ||
            distance < minimumJumpDistance)
        {
            return;
        }

        JumpTowardPlayer();
    }

    private void JumpTowardPlayer()
    {
        predictedTargetPosition =
            CalculatePredictedPosition();

        Vector3 direction =
            predictedTargetPosition - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = transform.forward;

        jumpDirection = direction.normalized;

        Vector3 velocity = rb.linearVelocity;

        velocity.x =
            jumpDirection.x * horizontalJumpSpeed;

        velocity.z =
            jumpDirection.z * horizontalJumpSpeed;

        velocity.y = jumpForce;

        rb.linearVelocity = velocity;

        isJumping = true;
        lastJumpTime = Time.time;

        if (showDebug)
        {
            Debug.Log(
                $"{name} jumped toward " +
                $"{predictedTargetPosition}"
            );
        }
    }

    // =====================================
    // PLAYER POSITION PREDICTION
    // =====================================

    private Vector3 CalculatePredictedPosition()
    {
        if (player == null)
            return transform.position;

        Vector3 targetPosition = player.position;

        if (enablePrediction && playerRigidbody != null)
        {
            Vector3 playerVelocity =
                playerRigidbody.linearVelocity;

            Vector3 horizontalVelocity =
                Vector3.ProjectOnPlane(
                    playerVelocity,
                    Vector3.up
                );

            float distance =
                Vector3.Distance(
                    transform.position,
                    player.position
                );

            float travelTime =
                distance /
                Mathf.Max(0.1f, horizontalJumpSpeed);

            travelTime = Mathf.Min(
                travelTime,
                maxPredictionTime
            );

            targetPosition +=
                horizontalVelocity *
                travelTime *
                predictionStrength;
        }

        // Randomness prevents multiple enemies
        // from always aiming at the same position.
        Vector2 randomOffset =
            Random.insideUnitCircle *
            predictionRandomness;

        targetPosition += new Vector3(
            randomOffset.x,
            0f,
            randomOffset.y
        );

        return targetPosition;
    }

    // =====================================
    // AIR MOVEMENT
    // =====================================

    private void HandleAirMovement()
    {
        Vector3 direction =
            predictedTargetPosition - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        Vector3 horizontalVelocity =
            new Vector3(
                rb.linearVelocity.x,
                0f,
                rb.linearVelocity.z
            );

        if (horizontalVelocity.magnitude >= maxAirSpeed)
            return;

        rb.AddForce(
            direction * airAcceleration,
            ForceMode.Acceleration
        );
    }

    // =====================================
    // DAMAGE / KNOCKBACK RECOVERY
    // =====================================

    private void OnDamaged(float damage)
    {
        // Only start a new recovery period
        // if the enemy isn't already recovering.
        if (knockbackTimer <= 0f)
        {
            knockbackTimer = knockbackRecoveryTime;
        }

        if (showDebug)
        {
            Debug.Log(
                $"{name} took {damage:F1} damage. " +
                $"Recovery: {knockbackTimer:F2}"
            );
        }
    }

    // =====================================
    // DEBUG GIZMOS
    // =====================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange
        );

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            jumpRange
        );

        if (groundCheck != null)
        {
            Gizmos.color = Color.green;

            Gizmos.DrawWireSphere(
                groundCheck.position,
                groundCheckRadius
            );
        }

        if (Application.isPlaying && drawPrediction)
        {
            Gizmos.color = Color.cyan;

            Gizmos.DrawWireSphere(
                predictedTargetPosition,
                0.35f
            );

            Gizmos.DrawLine(
                transform.position,
                predictedTargetPosition
            );
        }
    }
}
