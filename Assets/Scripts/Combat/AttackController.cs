using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlatformFighterController))]
[RequireComponent(typeof(PlatformFighterActor))]
[RequireComponent(typeof(FighterInput))]
public class AttackController : MonoBehaviour
{
    [Header("Data")]
    public AttackSet moves;
    public LayerMask hurtboxMask;

    [Header("Hit Shape")]
    [Tooltip("If ON, windows are tested as circles (hitbubbles). If OFF, uses original boxes.")]
    public bool useHitBubbles = true;

    [Tooltip("When converting window.size to a bubble, use max(size.x,size.y)/2 as radius. If OFF, uses average/2.")]
    public bool bubbleRadiusFromMaxAxis = true;

    [Header("Runtime Visuals (Game View)")]
    [Tooltip("Show hit bubbles/boxes in-game. Uses HitVisDrawer if present, otherwise line Debug.DrawLine.")]
    public bool showHitVis = true;

    [Tooltip("Toggle runtime hit visuals.")]
    public KeyCode toggleHitVisKey = KeyCode.H;

    [Range(8, 64)] public int circleSegments = 20;
    public Color hitVisColor = new Color(1f, 0f, 0f, 0.35f);

    [Header("Aerial Behavior")]
    [Tooltip("If true, aerial attacks stop their active frames as soon as you land, then apply landing lag.")]
    public bool aerialEndsOnLanding = true;

    [Header("FX (optional)")]
    public CharacterTint tint; // optional; safe if not assigned

    [Header("Scene Gizmos (Editor)")]
    public bool showGizmosInScene = true;
    public bool gizmoOnlyWhenSelected = true;
    public AttackClip gizmoPreviewClip;
    public bool gizmoUseLastPlayedWhenAvailable = true;
    public Color gizmoColor = new Color(1f, 0f, 0f, 0.6f);

    // --- Refs ---
    PlatformFighterController ctrl;
    PlatformFighterActor actor;
    FighterInput input;
    Animator anim;
    Rigidbody2D rb;

    // --- State ---
    bool busy;
    int facing => ctrl != null && ctrl.FacingRight ? 1 : -1;
    AttackClip _lastPlayedClip;

    void Awake()
    {
        ctrl  = GetComponent<PlatformFighterController>();
        actor = GetComponent<PlatformFighterActor>();
        input = GetComponent<FighterInput>();
        anim  = GetComponentInChildren<Animator>();
        rb    = GetComponent<Rigidbody2D>();
        if (!tint) tint = GetComponent<CharacterTint>() ?? GetComponentInChildren<CharacterTint>();
    }

    void OnEnable()
    {
        if (HitVisDrawer.Instance) HitVisDrawer.Instance.visible = showHitVis;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleHitVisKey))
        {
            showHitVis = !showHitVis;
            if (HitVisDrawer.Instance) HitVisDrawer.Instance.visible = showHitVis;
        }

        if (busy) return;

        if (moves == null)
        {
            if (input.AttackPressed || input.SmashPressed || input.SpecialPressed)
                Debug.LogWarning($"{name}: AttackController has no AttackSet assigned.");
            return;
        }

        if (input.SmashPressed)
        {
            var c = SelectSmash();
            if (c != null) StartCoroutine(DoAttack(c));
            return;
        }

        if (input.SpecialPressed)
        {
            var c = SelectSpecial();
            if (c != null) StartCoroutine(DoAttack(c));
            return;
        }

        if (input.AttackPressed)
        {
            var c = SelectNormal();
            if (c != null) StartCoroutine(DoAttack(c));
        }
    }

    AttackClip SelectNormal()
    {
        float x = input.Move.x, y = input.Move.y;

        if (!ctrl.Grounded)
        {
            if (y > input.dirThreshold)  return moves.upAir;
            if (y < -input.dirThreshold) return moves.dair;

            if (Mathf.Abs(x) > input.dirThreshold)
            {
                bool forward = (x > 0f && ctrl.FacingRight) || (x < 0f && !ctrl.FacingRight);
                return forward ? moves.nair : moves.bair; // swap to fair if you add it
            }
            return moves.nair;
        }
        else
        {
            if (y > input.dirThreshold)  return moves.upTilt;
            if (y < -input.dirThreshold) return moves.downTilt;
            return moves.tilt;
        }
    }

    AttackClip SelectSmash()
    {
        float x = input.Move.x, y = input.Move.y;

        if (!ctrl.Grounded)
        {
            if (y > input.dirThreshold)  return moves.upAir;
            if (y < -input.dirThreshold) return moves.dair;

            if (Mathf.Abs(x) > input.dirThreshold)
            {
                bool back = (x > 0f && !ctrl.FacingRight) || (x < 0f && ctrl.FacingRight);
                return back ? moves.bair : moves.nair;
            }
            return moves.nair;
        }
        else
        {
            if (y > input.dirThreshold)  return moves.upSmash;
            if (y < -input.dirThreshold) return moves.dsmash;
            return moves.fsmash;
        }
    }

    AttackClip SelectSpecial()
    {
        float x = input.Move.x, y = input.Move.y;
        if (y > input.dirThreshold)  return moves.upSpecial;
        if (y < -input.dirThreshold) return moves.downSpecial;
        if (Mathf.Abs(x) > input.dirThreshold) return moves.special;
        return moves.special;
    }

    IEnumerator DoAttack(AttackClip clip)
    {
        if (!clip) yield break;

        _lastPlayedClip = clip; // remember for Scene gizmos
        busy = true;

        if (!string.IsNullOrEmpty(clip.animatorTrigger) && anim)
            anim.SetTrigger(clip.animatorTrigger);

        // Startup (frame-accurate if Fixed Timestep = 1/60)
        for (int i = 0; i < clip.startupFrames; i++)
            yield return new WaitForFixedUpdate();

        bool startedInAir      = !ctrl.Grounded;
        bool landedDuringAlive = false;

        // Determine longest active window
        int maxActive = 0;
        for (int i = 0; i < clip.windows.Count; i++)
            maxActive = Mathf.Max(maxActive, clip.windows[i].startFrame + clip.windows[i].activeFrames - 1);

        // Per-attack state
        bool attackConsumedByHit = false; // set by Single windows that consume the attack
        var hitOnceThisAttack = new HashSet<PlatformFighterActor>(); // Single windows
        var lastHitTime = new Dictionary<(PlatformFighterActor target, int wi), float>(64); // Multi cadence

        // ACTIVE frames
        for (int f = 1; f <= maxActive; f++)
        {
            if (startedInAir && ctrl.Grounded && aerialEndsOnLanding)
            {
                landedDuringAlive = true;
                break;
            }

            if (attackConsumedByHit)
            {
                yield return new WaitForFixedUpdate();
                continue;
            }

            for (int wi = 0; wi < clip.windows.Count; wi++)
            {
                var w = clip.windows[wi];
                if (f < w.startFrame || f >= w.startFrame + w.activeFrames) continue;

                Vector2 center = (Vector2)(rb ? rb.position : (Vector2)transform.position)
                               + new Vector2(w.offset.x * facing, w.offset.y);

                if (useHitBubbles)
                {
                    float r = bubbleRadiusFromMaxAxis
                        ? Mathf.Max(w.size.x, w.size.y) * 0.5f
                        : (w.size.x + w.size.y) * 0.25f;

                    var hits = Physics2D.OverlapCircleAll(center, r, hurtboxMask);
                    foreach (var h in hits)
                    {
                        var t = h.GetComponentInParent<PlatformFighterActor>();
                        if (!t || t == actor) continue;

                        if (w.hitKind == HitKind.Single)
                        {
                            if (hitOnceThisAttack.Contains(t)) continue;
                            ApplyWindowHit(t, w);
                            hitOnceThisAttack.Add(t);
                            if (w.consumeAttackOnHit) attackConsumedByHit = true;
                        }
                        else // Multi
                        {
                            float interval = Mathf.Max(0.01f, w.multiHitInterval <= 0f ? 0.1f : w.multiHitInterval);
                            var key = (t, wi);
                            if (!lastHitTime.TryGetValue(key, out float last) || (Time.time - last) >= interval)
                            {
                                ApplyWindowHit(t, w);
                                lastHitTime[key] = Time.time;
                            }
                        }
                    }

                    if (showHitVis)
                    {
                        if (HitVisDrawer.Instance) HitVisDrawer.Instance.DrawCircleSolid(center, r, hitVisColor, circleSegments);
                        else DrawCircleOutline(center, r, Color.red, circleSegments);
                    }
                }
                else
                {
                    var hits = Physics2D.OverlapBoxAll(center, w.size, 0f, hurtboxMask);
                    foreach (var h in hits)
                    {
                        var t = h.GetComponentInParent<PlatformFighterActor>();
                        if (!t || t == actor) continue;

                        if (w.hitKind == HitKind.Single)
                        {
                            if (hitOnceThisAttack.Contains(t)) continue;
                            ApplyWindowHit(t, w);
                            hitOnceThisAttack.Add(t);
                            if (w.consumeAttackOnHit) attackConsumedByHit = true;
                        }
                        else
                        {
                            float interval = Mathf.Max(0.01f, w.multiHitInterval <= 0f ? 0.1f : w.multiHitInterval);
                            var key = (t, wi);
                            if (!lastHitTime.TryGetValue(key, out float last) || (Time.time - last) >= interval)
                            {
                                ApplyWindowHit(t, w);
                                lastHitTime[key] = Time.time;
                            }
                        }
                    }

                    if (showHitVis)
                    {
                        if (HitVisDrawer.Instance) HitVisDrawer.Instance.DrawBoxSolid(center, w.size, hitVisColor);
                        else DrawBoxOutline(center, w.size, Color.red);
                    }
                }
            }

            yield return new WaitForFixedUpdate();
        }

        // Endlag or landing lag
        if (landedDuringAlive && clip.landingLag > 0)
        {
            if (tint) tint.SetLandingLagTint(true);
            for (int i = 0; i < clip.landingLag; i++)
                yield return new WaitForFixedUpdate();
            if (tint) tint.SetLandingLagTint(false);
        }
        else
        {
            for (int i = 0; i < clip.endlagFrames; i++)
                yield return new WaitForFixedUpdate();
        }

        busy = false;
    }

    // -------- Helpers --------

    void ApplyWindowHit(PlatformFighterActor target, HitboxWindow w)
    {
        target.AddDamage(w.damage);

        // Determine Sakurai/Angle/Legacy mode
        float finalDegrees;
        bool relative = true;

        if (w.useSakuraiAngle)
        {
            // Use TARGET grounded state if we can find it; fall back to 'grounded' if unknown.
            bool targetGrounded = true;
            var tgtCtrl = target.GetComponentInParent<PlatformFighterController>();
            if (tgtCtrl != null) targetGrounded = tgtCtrl.Grounded;

            finalDegrees = targetGrounded ? w.sakuraiGroundAngleDeg : w.sakuraiAirAngleDeg;
            relative = true; // Sakurai is conceptually forward-relative in Smash
        }
        else if (w.useKbAngle)
        {
            finalDegrees = w.kbAngleDeg;
            relative = w.angleIsRelativeToFacing;
        }
        else
        {
            // Legacy XY fallback
            int face = (ctrl != null && ctrl.FacingRight) ? 1 : -1;
            Vector2 legacy = new Vector2(w.kbDir.x * face, w.kbDir.y);
            if (legacy.sqrMagnitude <= 0f) legacy = new Vector2(1 * face, 0);
            legacy.Normalize();
            target.ApplyKnockback(legacy, w.baseKB, w.growth);
            return;
        }

        // Convert degrees -> unit vector (apply facing if relative)
        int facingSign = (ctrl != null && ctrl.FacingRight) ? 1 : -1;
        Vector2 dir = GetKBDirFromAngle(finalDegrees, relative ? facingSign : 1);

        // Optional: Ground clamp (avoid downward/outward angles that glue to floor)
        var tgtCtrl2 = target.GetComponentInParent<PlatformFighterController>();
        bool tgtGrounded2 = tgtCtrl2 ? tgtCtrl2.Grounded : true;
        if (tgtGrounded2 && w.minUpDegreesOnGround > 0f)
        {
            // Compute the current angle after facing flip, then clamp to a minimum upward
            float worldDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // Convert to a 'forward-relative' frame to apply a simple clamp
            float relDeg = relative ? (worldDeg * Mathf.Sign(facingSign)) : worldDeg;

            if (relDeg < w.minUpDegreesOnGround)
            {
                relDeg = w.minUpDegreesOnGround;
                dir = GetKBDirFromAngle(relDeg, relative ? facingSign : 1);
            }
        }

        target.ApplyKnockback(dir, w.baseKB, w.growth);
    }

    static Vector2 GetKBDirFromAngle(float degrees, int facingSign)
    {
        float rad = degrees * Mathf.Deg2Rad;
        Vector2 d = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        d.x *= facingSign;
        if (d.sqrMagnitude > 0f) d.Normalize();
        else d = new Vector2(1 * facingSign, 0);
        return d;
    }

    void DrawCircleOutline(Vector2 c, float r, Color col, int seg)
    {
        if (seg < 3) seg = 3;
        float step = Mathf.PI * 2f / seg;
        Vector3 prev = c + new Vector2(Mathf.Cos(0f), Mathf.Sin(0f)) * r;
        for (int i = 1; i <= seg; i++)
        {
            float a = step * i;
            Vector3 next = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            Debug.DrawLine(prev, next, col, Time.fixedDeltaTime);
            prev = next;
        }
    }

    void DrawBoxOutline(Vector2 c, Vector2 s, Color col)
    {
        Vector2 h = s * 0.5f;
        Vector3 a = new Vector3(c.x - h.x, c.y - h.y);
        Vector3 b = new Vector3(c.x - h.x, c.y + h.y);
        Vector3 d = new Vector3(c.x + h.x, c.y - h.y);
        Vector3 e = new Vector3(c.x + h.x, c.y + h.y);
        Debug.DrawLine(a, b, col, Time.fixedDeltaTime);
        Debug.DrawLine(b, e, col, Time.fixedDeltaTime);
        Debug.DrawLine(e, d, col, Time.fixedDeltaTime);
        Debug.DrawLine(d, a, col, Time.fixedDeltaTime);
    }

    // -------- Scene Gizmos --------
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmosInScene || gizmoOnlyWhenSelected) return;
        DrawAttackGizmos();
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmosInScene || !gizmoOnlyWhenSelected) return;
        DrawAttackGizmos();
    }

    void DrawAttackGizmos()
    {
        AttackClip clipToDraw = gizmoPreviewClip;
        if (gizmoUseLastPlayedWhenAvailable && _lastPlayedClip != null)
            clipToDraw = _lastPlayedClip;

        if (clipToDraw == null) return;

        bool facingRight = ctrl != null ? ctrl.FacingRight : true;
        Gizmos.color = gizmoColor;

        foreach (var w in clipToDraw.windows)
        {
            Vector2 center = (Vector2)transform.position
                           + new Vector2(w.offset.x * (facingRight ? 1f : -1f), w.offset.y);

            if (useHitBubbles)
            {
                float r = bubbleRadiusFromMaxAxis
                    ? Mathf.Max(w.size.x, w.size.y) * 0.5f
                    : (w.size.x + w.size.y) * 0.25f;
                Gizmos.DrawWireSphere(center, r);
            }
            else
            {
                Gizmos.DrawWireCube(center, w.size);
            }
        }
    }
#endif
}
