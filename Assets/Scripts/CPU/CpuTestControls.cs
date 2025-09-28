using UnityEngine;

public class CpuTestControls : MonoBehaviour
{
    [Header("References")]
    public PlatformFighterController player;   // your controllable fighter
    public PlatformFighterController cpu;      // the dummy CPU fighter (already in scene)

    [Header("Hotkeys")]
    public KeyCode repositionKey = KeyCode.R;

    [Header("Placement")]
    [Tooltip("Extra gap to place between player & CPU.")]
    public float spacing = 0.2f;

    [Tooltip("Snap to ground using this mask (optional). Leave 0 to skip ground snapping.")]
    public LayerMask groundMask;

    [Tooltip("How far to search downward for ground when snapping.")]
    public float groundSnapDistance = 3f;

    void Update()
    {
        if (Input.GetKeyDown(repositionKey))
            RepositionCpuInFrontOfPlayer();
    }

    void RepositionCpuInFrontOfPlayer()
    {
        if (!player || !cpu) { Debug.LogWarning("CpuTestControls: assign player & cpu references."); return; }

        // Determine direction (player facing)
        int dir = player.FacingRight ? 1 : -1;

        // Compute widths from colliders to avoid overlap
        var pCol = player.GetComponent<Collider2D>();
        var cCol = cpu.GetComponent<Collider2D>();
        float pHalf = pCol ? pCol.bounds.extents.x : 0.4f;
        float cHalf = cCol ? cCol.bounds.extents.x : 0.4f;

        // Target X right in front of player
        Vector2 pPos = player.transform.position;
        float targetX = pPos.x + dir * (pHalf + cHalf + spacing);

        // Start with same Y as player
        float targetY = pPos.y;

        // Optional: snap to ground directly beneath target
        if (groundMask.value != 0)
        {
            Vector2 castStart = new Vector2(targetX, pPos.y + groundSnapDistance);
            var hit = Physics2D.Raycast(castStart, Vector2.down, groundSnapDistance * 2f, groundMask);
            if (hit.collider)
            {
                float cHalfY = cCol ? cCol.bounds.extents.y : 0.5f;
                targetY = hit.point.y + cHalfY + 0.001f;
            }
        }

        // Move CPU
        cpu.transform.position = new Vector3(targetX, targetY, cpu.transform.position.z);

        // Zero CPU velocity so it doesn't slide
        var rb = cpu.GetComponent<Rigidbody2D>();
        if (rb) rb.linearVelocity = Vector2.zero;

        // Make CPU face the player (optional – looks nicer)
        bool cpuShouldFaceRight = !player.FacingRight;
        var s = cpu.transform.localScale;
        s.x = Mathf.Abs(s.x) * (cpuShouldFaceRight ? 1f : -1f);
        cpu.transform.localScale = s;

        // Reset CPU internal states that might matter (optional clean-up)
        // e.g., clear any attack coroutines, drop momentum carry, etc., if you have public methods for that.
    }
}
