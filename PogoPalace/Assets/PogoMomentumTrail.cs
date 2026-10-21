
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PogoMomentumTrail : MonoBehaviour
{
    [Header("References")]
    public Rigidbody playerRigidbody;
    public TrailRenderer trailRenderer;

    [Header("Trail Settings")]
    public bool enableMomentumTrail = true;

    public float minTrailSpeed = 6f;
    public float maxTrailSpeed = 25f;

    [Header("Trail Width")]
    public float minTrailWidth = 0.08f;
    public float maxTrailWidth = 0.5f;

    public float widthSmoothness = 8f;

    [Header("Trail Duration")]
    public float trailLifetime = 0.35f;

    [Header("Debug")]
    public bool showDebug = false;

    private float currentWidth;

    private void Awake()
    {
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();

        if (trailRenderer == null)
            trailRenderer = GetComponentInChildren<TrailRenderer>();

        if (trailRenderer == null)
        {
            Debug.LogWarning(
                "PogoMomentumTrail: No TrailRenderer assigned!"
            );
            enabled = false;
            return;
        }

        currentWidth = minTrailWidth;

        trailRenderer.emitting = false;
        trailRenderer.time = trailLifetime;
        trailRenderer.widthMultiplier = currentWidth;
    }

    private void LateUpdate()
    {
        if (playerRigidbody == null ||
            trailRenderer == null)
            return;

        if (!enableMomentumTrail)
        {
            trailRenderer.emitting = false;
            return;
        }

        // Get the player's total movement speed.
        float speed =
            playerRigidbody.linearVelocity.magnitude;

        // Only show the trail above the minimum speed.
        bool shouldEmit = speed >= minTrailSpeed;

        trailRenderer.emitting = shouldEmit;

        if (!shouldEmit)
            return;

        // Convert speed into a normalized value.
        float speedPercent = Mathf.InverseLerp(
            minTrailSpeed,
            maxTrailSpeed,
            speed
        );

        // Calculate desired trail thickness.
        float targetWidth = Mathf.Lerp(
            minTrailWidth,
            maxTrailWidth,
            speedPercent
        );

        // Smooth the width changes.
        currentWidth = Mathf.Lerp(
            currentWidth,
            targetWidth,
            1f - Mathf.Exp(
                -widthSmoothness * Time.deltaTime
            )
        );

        // Apply the width.
        trailRenderer.widthMultiplier = currentWidth;

        // Keep trail lifetime updated.
        trailRenderer.time = trailLifetime;

        if (showDebug)
        {
            Debug.Log(
                $"Speed: {speed:F1} | Trail Width: {currentWidth:F2}"
            );
        }
        if (!shouldEmit)
        {
            currentWidth = minTrailWidth;
            trailRenderer.widthMultiplier = currentWidth;
            return;
        }
    }

    public void ClearTrail()
    {
        if (trailRenderer == null)
            return;

        trailRenderer.Clear();
        trailRenderer.emitting = false;
        currentWidth = minTrailWidth;
        trailRenderer.widthMultiplier = currentWidth;
    }
}
