
using UnityEngine;
using UnityEngine.InputSystem;

public class PogoCamera : MonoBehaviour
{
    [Header("References")]
    public Transform target;
    public InputActionReference lookAction;

    [Header("Camera")]
    public float distance = 6f;
    public float height = 1.5f;
    public float sensitivity = 0.15f;
    public float followSpeed = 10f;
    public float minPitch = -25f;
    public float maxPitch = 75f;

    [Header("Speed-Based Camera Shake")]
    public bool enableSpeedShake = true;

    public Rigidbody playerRigidbody;

    public float minShakeSpeed = 5f;
    public float maxShakeSpeed = 25f;

    public float minShakeIntensity = 0f;
    public float maxShakeIntensity = 0.12f;

    public float minShakeFrequency = 5f;
    public float maxShakeFrequency = 18f;

    public float shakeSmoothness = 5f;

    private float currentShakeIntensity;
    private float shakeTime;

    [Header("Camera Collision")]
    public bool enableCameraCollision = true;
    public LayerMask cameraCollisionMask;

    public float cameraCollisionRadius = 0.25f;
    public float cameraCollisionOffset = 0.15f;

    public float cameraMoveInSpeed = 25f;
    public float cameraMoveOutSpeed = 5f;

    public float minimumCameraDistance = 0.4f;

    private float yaw;
    private float pitch = 15f;

    private float currentCameraDistance;

    // Smoothed position that the camera orbits around.
    private Vector3 smoothedTargetPosition;

    void OnEnable()
    {
        lookAction?.action.Enable();
    }

    void OnDisable()
    {
        lookAction?.action.Disable();
    }

    void Start()
    {
        yaw = transform.eulerAngles.y;

        if (target != null)
        {
            smoothedTargetPosition =
                target.position + Vector3.up * height;
        }

        currentCameraDistance = distance;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        // ---------------------------------
        // MOUSE LOOK
        // ---------------------------------

        Vector2 look = Vector2.zero;

        if (lookAction != null)
        {
            look = lookAction.action.ReadValue<Vector2>();
        }

        yaw += look.x * sensitivity;
        pitch -= look.y * sensitivity;

        pitch = Mathf.Clamp(
            pitch,
            minPitch,
            maxPitch
        );

        Quaternion rotation = Quaternion.Euler(
            pitch,
            yaw,
            0f
        );

        // ---------------------------------
        // SMOOTH FOLLOW TARGET
        // ---------------------------------

        Vector3 targetPosition =
            target.position + Vector3.up * height;

        float followLerp = 1f - Mathf.Exp(
            -followSpeed * Time.deltaTime
        );

        smoothedTargetPosition = Vector3.Lerp(
            smoothedTargetPosition,
            targetPosition,
            followLerp
        );

        // ---------------------------------
        // DESIRED CAMERA POSITION
        // ---------------------------------

        Vector3 desiredPosition =
            smoothedTargetPosition -
            rotation * Vector3.forward * distance;

        // ---------------------------------
        // CAMERA COLLISION
        // ---------------------------------

        Vector3 finalPosition = ResolveCameraCollision(
            smoothedTargetPosition,
            desiredPosition,
            Time.deltaTime
        );

        // ---------------------------------
        // APPLY CAMERA TRANSFORM
        // ---------------------------------

        // Calculate speed-based shake.
        Vector3 shakeOffset = CalculateSpeedShake();

        // Convert local shake into world space.
        Vector3 worldShakeOffset = rotation * shakeOffset;

        // Apply camera position and rotation.
        transform.position = finalPosition + worldShakeOffset;
        transform.rotation = rotation;
    }

    private Vector3 ResolveCameraCollision(
        Vector3 followPosition,
        Vector3 desiredCameraPosition,
        float deltaTime)
    {
        Vector3 direction =
            desiredCameraPosition - followPosition;

        float desiredDistance = direction.magnitude;

        if (desiredDistance < 0.001f)
            return desiredCameraPosition;

        direction /= desiredDistance;

        // If collision is disabled, use the normal
        // camera position.
        if (!enableCameraCollision)
        {
            currentCameraDistance = desiredDistance;
            return desiredCameraPosition;
        }

        float targetDistance = desiredDistance;

        // ---------------------------------
        // DETECT OBSTRUCTIONS
        // ---------------------------------

        if (Physics.SphereCast(
            followPosition,
            cameraCollisionRadius,
            direction,
            out RaycastHit hit,
            desiredDistance,
            cameraCollisionMask,
            QueryTriggerInteraction.Ignore))
        {
            targetDistance = Mathf.Max(
                0f,
                hit.distance - cameraCollisionOffset
            );
        }

        // Allow the minimum distance only when
        // there is sufficient space. Do not force
        // the camera through a nearby wall.
        if (targetDistance >= minimumCameraDistance)
        {
            targetDistance = Mathf.Max(
                minimumCameraDistance,
                targetDistance
            );
        }

        // ---------------------------------
        // CAMERA DISTANCE SMOOTHING
        // ---------------------------------

        if (targetDistance < currentCameraDistance)
        {
            // Instantly move closer when blocked.
            currentCameraDistance = targetDistance;
        }
        else
        {
            // Smoothly move back when clear.
            currentCameraDistance = Mathf.MoveTowards(
                currentCameraDistance,
                targetDistance,
                cameraMoveOutSpeed * deltaTime
            );
        }

        currentCameraDistance = Mathf.Clamp(
            currentCameraDistance,
            0f,
            desiredDistance
        );

        // ---------------------------------
        // FINAL CAMERA POSITION
        // ---------------------------------

        return followPosition +
               direction * currentCameraDistance;
    }


    private Vector3 CalculateSpeedShake()
    {
        if (!enableSpeedShake || playerRigidbody == null)
        {
            currentShakeIntensity = Mathf.MoveTowards(
                currentShakeIntensity,
                0f,
                shakeSmoothness * Time.deltaTime
            );

            return Vector3.zero;
        }

        // Get the player's total movement speed.
        float speed = playerRigidbody.linearVelocity.magnitude;

        // Convert speed into a value between 0 and 1.
        float speedPercent = Mathf.InverseLerp(
            minShakeSpeed,
            maxShakeSpeed,
            speed
        );

        // Smoothly increase shake intensity.
        float targetIntensity = Mathf.Lerp(
            minShakeIntensity,
            maxShakeIntensity,
            speedPercent
        );

        currentShakeIntensity = Mathf.Lerp(
            currentShakeIntensity,
            targetIntensity,
            1f - Mathf.Exp(
                -shakeSmoothness * Time.deltaTime
            )
        );

        // Shake frequency also increases with speed.
        float frequency = Mathf.Lerp(
            minShakeFrequency,
            maxShakeFrequency,
            speedPercent
        );

        shakeTime += Time.deltaTime * frequency;

        // Smooth Perlin noise for horizontal and vertical shake.
        float shakeX = Mathf.PerlinNoise(
            shakeTime,
            0f
        ) * 2f - 1f;

        float shakeY = Mathf.PerlinNoise(
            0f,
            shakeTime
        ) * 2f - 1f;

        return new Vector3(
            shakeX,
            shakeY,
            0f
        ) * currentShakeIntensity;
    }


    private void OnDrawGizmosSelected()
    {
        if (target == null)
            return;

        Vector3 pivot =
            target.position + Vector3.up * height;

        Vector3 direction = -transform.forward;

        Gizmos.color = Color.yellow;

        Gizmos.DrawLine(
            pivot,
            pivot + direction * distance
        );

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            transform.position,
            cameraCollisionRadius
        );
    }
}
