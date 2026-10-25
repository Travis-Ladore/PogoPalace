
using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public bool destroyOnDeath = true;
    public float destroyDelay = 0.1f;

    [Header("Knockback")]
    public Rigidbody rb;
    public bool allowKnockback = true;

    [Header("Debug")]
    public bool showDebug = false;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    public event Action<EnemyHealth> OnDeath;
    public event Action<float> OnDamaged;

    private void Awake()
    {
        CurrentHealth = maxHealth;

        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();
    }

    public void TakeDamage(
        float damage,
        Vector3 knockbackDirection,
        float knockbackForce)
    {
        if (IsDead || damage <= 0f)
            return;

        CurrentHealth = Mathf.Max(
            0f,
            CurrentHealth - damage
        );

        OnDamaged?.Invoke(damage);

        if (allowKnockback &&
            rb != null &&
            !rb.isKinematic)
        {
            Vector3 direction =
                knockbackDirection.normalized;

            rb.AddForce(
                direction * knockbackForce,
                ForceMode.Impulse
            );
        }

        if (showDebug)
        {
            Debug.Log(
                $"{name} took {damage:F1} damage. " +
                $"Health: {CurrentHealth:F1}"
            );
        }

        if (CurrentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (IsDead)
            return;

        IsDead = true;
        OnDeath?.Invoke(this);

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }
}
