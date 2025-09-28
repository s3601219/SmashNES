using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlatformFighterActor : MonoBehaviour
{
    public float percent { get; private set; } = 0f;
    Rigidbody2D rb;

    void Awake() { rb = GetComponent<Rigidbody2D>(); }

    public void AddDamage(float amount) => percent += amount;

    // Minimal smash-like knockback: scales with attack power and current %
    public void ApplyKnockback(Vector2 dir, float baseKB, float growth)
    {
        dir = dir.normalized;
        float k = baseKB + percent * growth;   // tweak to taste
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dir * k, ForceMode2D.Impulse);
    }

    public void ResetPercent() => percent = 0f;
}
