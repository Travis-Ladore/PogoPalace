
using System.Collections.Generic;
using UnityEngine;

public class PogoLandingShockwave : MonoBehaviour
{
    [Header("References")]
    public Transform pogoTip;

    [Header("Enemy Detection")]
    public LayerMask enemyMask;
    public float shockwaveRadius = 4f;

    [Header("Damage")]
    public float baseDamage = 10f;
    public float damagePerImpactSpeed = 1.5f;
    public float minimumImpactSpeed = 4f;
    public float maxDamage = 40f;

    [Header("Knockback")]
    public float knockbackForce = 8f;
    public float upwardKnockback = 0.35f;

    [Header("Debug")]
    public bool showDebug = false;

    public void TriggerShockwave(float impactSpeed)
    {
        if (impactSpeed < minimumImpactSpeed)
            return;

        Vector3 center = pogoTip != null
            ? pogoTip.position
            : transform.position;

        float damage = Mathf.Clamp(
            baseDamage +
            (impactSpeed - minimumImpactSpeed) *
            damagePerImpactSpeed,
            baseDamage,
            maxDamage
        );

        Collider[] hits = Physics.OverlapSphere(
            center,
            shockwaveRadius,
            enemyMask,
            QueryTriggerInteraction.Ignore
        );

        // Prevent enemies with multiple colliders
        // from receiving duplicate damage.
        HashSet<EnemyHealth> damagedEnemies =
            new HashSet<EnemyHealth>();

        foreach (Collider hit in hits)
        {
            EnemyHealth enemy =
                hit.GetComponentInParent<EnemyHealth>();

            if (enemy == null ||
                enemy.IsDead ||
                !damagedEnemies.Add(enemy))
            {
                continue;
            }

            Vector3 direction =
                enemy.transform.position - center;

            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                direction = transform.forward;

            direction.Normalize();
            direction.y = upwardKnockback;
            direction.Normalize();

            enemy.TakeDamage(
                damage,
                direction,
                knockbackForce
            );

            if (showDebug)
            {
                Debug.Log(
                    $"Shockwave hit {enemy.name} " +
                    $"for {damage:F1} damage."
                );
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Vector3 center = pogoTip != null
            ? pogoTip.position
            : transform.position;

        Gizmos.DrawWireSphere(
            center,
            shockwaveRadius
        );
    }
}
