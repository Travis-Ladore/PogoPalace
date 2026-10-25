
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PogoCombat : MonoBehaviour
{
    [Header("References")]
    public Transform pogoTip;
    public Rigidbody playerRigidbody;

    [Header("Enemy Detection")]
    public LayerMask enemyMask;
    public float stompRadius = 0.3f;
    public float stompDistance = 0.35f;

    [Header("Stomp Damage")]
    public float minimumStompSpeed = 3f;
    public float baseStompDamage = 25f;
    public float damagePerSpeed = 3f;
    public float maxStompDamage = 100f;

    [Header("Stomp Rebound")]
    public bool reboundOnStomp = true;
    public float reboundSpeed = 9f;

    [Header("Enemy Knockback")]
    public float stompKnockback = 5f;

    [Header("Debug")]
    public bool showDebug = false;

    private EnemyHealth lastStompTarget;
    private float lastStompTime = -100f;

    private void Awake()
    {
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        CheckStomp();
    }

    private void CheckStomp()
    {
        if (pogoTip == null || playerRigidbody == null)
            return;

        Vector3 downwardAxis = -transform.up;

        Vector3 tipVelocity =
            playerRigidbody.GetPointVelocity(
                pogoTip.position
            );

        float downwardSpeed =
            Vector3.Dot(tipVelocity, downwardAxis);

        if (downwardSpeed < minimumStompSpeed)
            return;

        // Avoid repeat hits from the same physics contact.
        if (Time.time - lastStompTime < 0.2f)
            return;

        Vector3 castStart =
            pogoTip.position - downwardAxis * 0.1f;

        if (!Physics.SphereCast(
            castStart,
            stompRadius,
            downwardAxis,
            out RaycastHit hit,
            stompDistance,
            enemyMask,
            QueryTriggerInteraction.Ignore))
        {
            return;
        }

        EnemyHealth enemy =
            hit.collider.GetComponentInParent<EnemyHealth>();

        if (enemy == null || enemy.IsDead)
            return;

        float damage = Mathf.Clamp(
            baseStompDamage +
            (downwardSpeed - minimumStompSpeed) *
            damagePerSpeed,
            baseStompDamage,
            maxStompDamage
        );

        Vector3 knockbackDirection =
            (enemy.transform.position -
             transform.position).normalized;

        knockbackDirection.y = 0.3f;
        knockbackDirection.Normalize();

        enemy.TakeDamage(
            damage,
            knockbackDirection,
            stompKnockback
        );

        lastStompTarget = enemy;
        lastStompTime = Time.time;

        if (reboundOnStomp)
        {
            Vector3 velocity =
                playerRigidbody.linearVelocity;

            velocity.y = Mathf.Max(
                velocity.y,
                reboundSpeed
            );

            playerRigidbody.linearVelocity = velocity;
        }

        if (showDebug)
        {
            Debug.Log(
                $"Pogo stomped {enemy.name} " +
                $"for {damage:F1} damage!"
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (pogoTip == null)
            return;

        Gizmos.color = Color.red;

        Vector3 down = -transform.up;

        Gizmos.DrawWireSphere(
            pogoTip.position + down * stompDistance,
            stompRadius
        );
    }
}
