using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlatformFighterActor : MonoBehaviour
{
    public float percent { get; private set; } = 0f;
    public bool InHitstun => _hitstunTime > 0f;

    Rigidbody2D rb;

    [Header("Knockback / Hitstun")]
    [Tooltip("Global scale on all knockback speeds.")]
    public float knockbackScale = 1f;

    [Tooltip("Seconds of hitstun added per 1 unit of knockback magnitude.")]
    public float hitstunSecondsPerKB = 0.05f;

    [Tooltip("Minimum hitstun seconds for any hit (0 = none).")]
    public float minHitstun = 0.00f;

    [Tooltip("Maximum hitstun seconds clamp (0 = no clamp).")]
    public float maxHitstun = 0.00f;

    float _hitstunTime = 0f;

    void Awake() { rb = GetComponent<Rigidbody2D>(); }

    void FixedUpdate()
    {
        if (_hitstunTime > 0f)
            _hitstunTime -= Time.fixedDeltaTime;
    }

    public void AddDamage(float amount) => percent += amount;

    // Launch exactly along 'dir' with Smash-like magnitude; also start hitstun.
    public void ApplyKnockback(Vector2 dir, float baseKB, float growth)
    {
        if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
        dir.Normalize();

        float k = (baseKB + percent * growth) * Mathf.Max(0.0001f, knockbackScale);

        // Set the exact launch velocity (avoid AddForce so X doesn't get eaten by gravity/friction integration)
        rb.linearVelocity = dir * k;

        // Simple hitstun based on KB magnitude
        float stun = k * hitstunSecondsPerKB;
        if (stun < minHitstun) stun = minHitstun;
        if (maxHitstun > 0f && stun > maxHitstun) stun = maxHitstun;
        _hitstunTime = stun;

        // Debug: confirm launch vector
        // Debug.Log($"{name} launched with {dir} * {k} = {rb.linearVelocity}");
    }

    public void ResetPercent() => percent = 0f;
}
