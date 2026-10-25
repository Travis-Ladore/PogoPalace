
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PogoLandingEffect : MonoBehaviour
{
    [Header("References")]
    public Rigidbody playerRigidbody;
    public PogoSpring pogoSpring;
    public Transform pogoTip;

    [Header("Ground Detection")]
    public LayerMask groundMask;
    public float groundCheckDistance = 0.35f;
    public float groundCheckRadius = 0.12f;
    public float contactOffset = 0.03f;

    [Header("Impact Speed")]
    public float minimumImpactSpeed = 2f;
    public float maximumImpactSpeed = 25f;

    [Header("Particle Amount")]
    public int minParticles = 8;
    public int maxParticles = 35;

    [Header("Combat")]
    public PogoLandingShockwave landingShockwave;

    [Header("Particle Size")]
    public float minParticleSize = 0.1f;
    public float maxParticleSize = 0.4f;

    [Header("Particle Movement")]
    public float minBurstSpeed = 2f;
    public float maxBurstSpeed = 8f;
    public float particleLifetime = 1.5f;
    public float upwardBurstAmount = 0.5f;
    public float particleGravity = 1f;

    [Header("Ground Collision")]
    public bool enableParticleCollision = true;

    [Range(0f, 1f)]
    public float groundBounce = 0.05f;

    [Range(0f, 1f)]
    public float groundDampen = 0.8f;

    public float particleCollisionRadiusScale = 0.5f;

    [Header("Particle Appearance")]
    public Mesh cubeMesh;
    public Material particleMaterial;
    public Color fallbackColor = Color.magenta;

    [Header("Debug")]
    public bool showDebug = false;

    [SerializeField]
    private float currentImpactSpeed;

    [SerializeField]
    private bool isGrounded;

    private ParticleSystem landingParticles;
    private ParticleSystemRenderer particleRenderer;

    private bool wasGrounded;
    private float previousVerticalSpeed;

    private void Awake()
    {
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();

        if (pogoSpring == null)
            pogoSpring = GetComponent<PogoSpring>();

        if (landingShockwave == null)
            landingShockwave =
                GetComponent<PogoLandingShockwave>();

        CreateParticleSystem();
    }

    private void FixedUpdate()
    {
        if (playerRigidbody == null || pogoTip == null)
            return;

        // Measure the velocity of the pogo tip.
        Vector3 tipVelocity =
            playerRigidbody.GetPointVelocity(
                pogoTip.position
            );

        // Detect ground immediately below the pogo tip.
        bool touchingGround = Physics.SphereCast(
            pogoTip.position + transform.up * 0.1f,
            groundCheckRadius,
            -transform.up,
            out RaycastHit hit,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        isGrounded = touchingGround;

        // Only trigger on the first frame of a landing.
        if (touchingGround && !wasGrounded)
        {
            float impactSpeed = Mathf.Max(
                0f,
                -Vector3.Dot(tipVelocity, hit.normal)
            );

            currentImpactSpeed = impactSpeed;

            if (impactSpeed >= minimumImpactSpeed)
            {
                float impactPercent = Mathf.InverseLerp(
                    minimumImpactSpeed,
                    maximumImpactSpeed,
                    impactSpeed
                );

                Color surfaceColor =
                    GetSurfaceColor(hit);

                // Existing cube debris effect.
                SpawnParticles(
                    hit.point,
                    hit.normal,
                    surfaceColor,
                    impactPercent
                );

                // NEW: Damage and knock back nearby enemies.
                // Uses the same impact speed as the particles.
                if (landingShockwave != null)
                {
                    landingShockwave.TriggerShockwave(
                        impactSpeed
                    );
                }

                if (showDebug)
                {
                    Debug.Log(
                        $"Pogo landed at {hit.point}, " +
                        $"Impact: {impactSpeed:F2}, " +
                        $"Color: {surfaceColor}"
                    );
                }
            }
        }

        wasGrounded = touchingGround;

        previousVerticalSpeed =
            playerRigidbody.linearVelocity.y;
    }

    private void CreateParticleSystem()
    {
        GameObject particleObject =
            new GameObject("LandingCubeParticles");

        particleObject.transform.SetParent(
            transform,
            false
        );

        landingParticles =
            particleObject.AddComponent<ParticleSystem>();

        particleRenderer =
            particleObject.GetComponent<ParticleSystemRenderer>();

        var main = landingParticles.main;

        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = particleLifetime;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.startColor = Color.white;
        main.maxParticles = Mathf.Max(
            100,
            maxParticles * 5
        );

        // World simulation prevents emitted cubes
        // from following the pogo after landing.
        main.simulationSpace =
            ParticleSystemSimulationSpace.World;

        main.gravityModifier = particleGravity;

        var emission = landingParticles.emission;
        emission.enabled = false;

        var shape = landingParticles.shape;
        shape.enabled = false;

        // Make cubes collide with the ground.
        var collision = landingParticles.collision;

        collision.enabled = enableParticleCollision;
        collision.type =
            ParticleSystemCollisionType.World;

        collision.mode =
            ParticleSystemCollisionMode.Collision3D;

        collision.collidesWith = groundMask;
        collision.bounce = groundBounce;
        collision.dampen = groundDampen;
        collision.radiusScale =
            particleCollisionRadiusScale;

        collision.lifetimeLoss = 0f;
        collision.quality =
            ParticleSystemCollisionQuality.High;

        particleRenderer.renderMode =
            ParticleSystemRenderMode.Mesh;

        particleRenderer.mesh =
            cubeMesh != null
                ? cubeMesh
                : CreateCubeMesh();

        if (particleMaterial != null)
        {
            particleRenderer.sharedMaterial =
                particleMaterial;
        }
        else
        {
            Debug.LogWarning(
                "PogoLandingEffect: Assign a particle material."
            );
        }

        landingParticles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }

    private Mesh CreateCubeMesh()
    {
        GameObject cube =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        Mesh mesh =
            cube.GetComponent<MeshFilter>().sharedMesh;

        Destroy(cube);

        return mesh;
    }

    private Color GetSurfaceColor(RaycastHit hit)
    {
        Renderer surfaceRenderer =
            hit.collider.GetComponent<Renderer>();

        if (surfaceRenderer == null)
        {
            surfaceRenderer =
                hit.collider.GetComponentInParent<Renderer>();
        }

        if (surfaceRenderer == null)
            return fallbackColor;

        Material material =
            surfaceRenderer.sharedMaterial;

        if (material == null)
            return fallbackColor;

        Color materialColor = Color.white;

        if (material.HasProperty("_BaseColor"))
        {
            materialColor =
                material.GetColor("_BaseColor");
        }
        else if (material.HasProperty("_Color"))
        {
            materialColor =
                material.GetColor("_Color");
        }

        // Try sampling the texture at the collision point.
        Texture texture = null;

        if (material.HasProperty("_BaseMap"))
        {
            texture =
                material.GetTexture("_BaseMap");
        }
        else if (material.HasProperty("_MainTex"))
        {
            texture =
                material.GetTexture("_MainTex");
        }

        if (texture is Texture2D texture2D)
        {
            try
            {
                Vector2 uv = hit.textureCoord;

                Vector2 tiling =
                    material.mainTextureScale;

                Vector2 offset =
                    material.mainTextureOffset;

                uv = Vector2.Scale(
                    uv,
                    tiling
                ) + offset;

                Color textureColor =
                    texture2D.GetPixelBilinear(
                        uv.x,
                        uv.y
                    );

                return textureColor * materialColor;
            }
            catch (UnityException)
            {
                if (showDebug)
                {
                    Debug.LogWarning(
                        "PogoLandingEffect: Ground texture " +
                        "is not readable. Using material " +
                        "color instead."
                    );
                }
            }
        }

        return materialColor;
    }

    private void SpawnParticles(
        Vector3 position,
        Vector3 normal,
        Color color,
        float impactPercent)
    {
        int count = Mathf.RoundToInt(
            Mathf.Lerp(
                minParticles,
                maxParticles,
                impactPercent
            )
        );

        float size = Mathf.Lerp(
            minParticleSize,
            maxParticleSize,
            impactPercent
        );

        float speed = Mathf.Lerp(
            minBurstSpeed,
            maxBurstSpeed,
            impactPercent
        );

        Vector3 spawnPosition =
            position + normal * contactOffset;

        for (int i = 0; i < count; i++)
        {
            Vector3 randomDirection =
                Random.onUnitSphere;

            Vector3 tangent =
                Vector3.ProjectOnPlane(
                    randomDirection,
                    normal
                ).normalized;

            if (tangent.sqrMagnitude < 0.001f)
            {
                tangent = Vector3.Cross(
                    normal,
                    Vector3.forward
                ).normalized;
            }

            Vector3 velocity =
                tangent * speed +
                normal * speed * upwardBurstAmount;

            ParticleSystem.EmitParams particle =
                new ParticleSystem.EmitParams();

            particle.position = spawnPosition;

            particle.velocity =
                velocity * Random.Range(
                    0.6f,
                    1.2f
                );

            particle.startSize =
                size * Random.Range(
                    0.7f,
                    1.3f
                );

            particle.startColor = color;

            particle.startLifetime =
                particleLifetime * Random.Range(
                    0.7f,
                    1.2f
                );

            landingParticles.Emit(
                particle,
                1
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (pogoTip == null)
            return;

        Gizmos.color = Color.yellow;

        Vector3 origin =
            pogoTip.position +
            transform.up * 0.1f;

        Gizmos.DrawWireSphere(
            origin,
            groundCheckRadius
        );

        Gizmos.DrawLine(
            origin,
            origin -
            transform.up * groundCheckDistance
        );
    }
}
