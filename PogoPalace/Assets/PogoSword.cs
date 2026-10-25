
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PogoSword : MonoBehaviour
{
    [Header("References")]
    public Rigidbody playerRigidbody;
    public Transform swordTip;

    [Header("Damage")]
    public float minimumHitSpeed = 2f;
    public float baseDamage = 10f;
    public float damagePerSpeed = 4f;
    public float maxDamage = 80f;

    [Header("Knockback")]
    public float baseKnockback = 3f;
    public float knockbackPerSpeed = 0.5f;
    public float upwardKnockback = 0.2f;

    [Header("Hit Detection")]
    public LayerMask enemyMask;
    public float rehitCooldown = 0.35f;

    [Header("Debug")]
    public bool showDebug = false;
    public bool drawVelocity = true;

    private Collider swordCollider;

    // Tracks enemies currently touching the blade.
    private readonly HashSet<EnemyHealth> touchingEnemies =
        new HashSet<EnemyHealth>();

    // Prevents rapid repeat damage from physics jitter.
    private readonly Dictionary<EnemyHealth, float> lastHitTimes =
        new Dictionary<EnemyHealth, float>();

    private Vector3 previousTipPosition;
    private Vector3 currentBladeVelocity;

    private void Awake()
    {
        swordCollider = GetComponent<Collider>();
        swordCollider.isTrigger = true;

        if (playerRigidbody == null)
        {
            playerRigidbody =
                GetComponentInParent<Rigidbody>();
        }
    }

    private void Start()
    {
        previousTipPosition = GetBladePosition();
    }

    private void FixedUpdate()
    {
        Vector3 currentPosition = GetBladePosition();

        currentBladeVelocity =
            (currentPosition - previousTipPosition) /
            Time.fixedDeltaTime;

        previousTipPosition = currentPosition;
    }

    private Vector3 GetBladePosition()
    {
        return swordTip != null
            ? swordTip.position
            : transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHitEnemy(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Handles contacts that begin between physics steps
        // or were initially too slow to cause damage.
        TryHitEnemy(other);
    }

    private void OnTriggerExit(Collider other)
    {
        EnemyHealth enemy =
            other.GetComponentInParent<EnemyHealth>();

        if (enemy != null)
            touchingEnemies.Remove(enemy);
    }

    private void TryHitEnemy(Collider other)
    {
        if ((enemyMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        EnemyHealth enemy =
            other.GetComponentInParent<EnemyHealth>();

        if (enemy == null || enemy.IsDead)
            return;

        // Only one hit per continuous contact.
        if (touchingEnemies.Contains(enemy))
            return;

        if (lastHitTimes.TryGetValue(
            enemy,
            out float lastHitTime))
        {
            if (Time.time - lastHitTime < rehitCooldown)
                return;
        }

        Vector3 hitPoint =
            other.ClosestPoint(GetBladePosition());

        // Rigidbody.GetPointVelocity includes angular velocity.
        // This is important for spinning pogo attacks.
        Vector3 bladeVelocity =
            playerRigidbody != null
                ? playerRigidbody.GetPointVelocity(hitPoint)
                : currentBladeVelocity;

        // Relative velocity makes moving enemies count.
        Rigidbody enemyBody =
            enemy.GetComponentInParent<Rigidbody>();

        Vector3 enemyVelocity =
            enemyBody != null
                ? enemyBody.GetPointVelocity(hitPoint)
                : Vector3.zero;

        Vector3 relativeVelocity =
            bladeVelocity - enemyVelocity;

        float hitSpeed = relativeVelocity.magnitude;

        if (hitSpeed < minimumHitSpeed)
            return;

        float damage = Mathf.Clamp(
            baseDamage +
            (hitSpeed - minimumHitSpeed) * damagePerSpeed,
            baseDamage,
            maxDamage
        );

        Vector3 knockbackDirection =
            relativeVelocity.normalized;

        knockbackDirection.y += upwardKnockback;
        knockbackDirection.Normalize();

        float knockback =
            baseKnockback +
            hitSpeed * knockbackPerSpeed;

        enemy.TakeDamage(
            damage,
            knockbackDirection,
            knockback
        );

        touchingEnemies.Add(enemy);
        lastHitTimes[enemy] = Time.time;

        if (showDebug)
        {
            Debug.Log(
                $"Sword hit {enemy.name}! " +
                $"Speed: {hitSpeed:F2} " +
                $"Damage: {damage:F1}"
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawVelocity)
            return;

        Vector3 position = GetBladePosition();

        Gizmos.color = Color.cyan;

        if (Application.isPlaying)
        {
            Gizmos.DrawRay(
                position,
                currentBladeVelocity * 0.15f
            );
        }
    }
}
