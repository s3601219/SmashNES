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
    public bool showHitVis = true;
    public KeyCode toggleHitVisKey = KeyCode.H;
    [Range(8, 64)] public int circleSegments = 20;
    public Color hitVisColor = new Color(1f, 0f, 0f, 0.35f);

    [Header("Aerial Behavior")]
    [Tooltip("If true, aerial attacks stop their active frames as soon as you land, then apply landing lag.")]
    public bool aerialEndsOnLanding = true;

    [Header("FX (optional)")]
    public CharacterTint tint; // optional

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

        // Block attacks during landing lag or hard lock
        if (busy || (ctrl != null && (ctrl.InLandingLag || ctrl.HardLocked))) return;

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
                return forward ? moves.nair : moves.bair;
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

    void DrawCircle(Vector2 center, float radius, Color color, int segments = 16)
    {
        float angleStep = 2f * Mathf.PI / segments;
        Vector3 prev = center + new Vector2(Mathf.Cos(0), Mathf.Sin(0)) * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep;
            Vector3 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Debug.DrawLine(prev, next, color, Time.fixedDeltaTime);
            prev = next;
        }
    }

    IEnumerator DoAttack(AttackClip clip)
    {
        if (clip == null) yield break;

        busy = true;
        _lastPlayedClip = clip;

        if (!string.IsNullOrEmpty(clip.animatorTrigger) && anim)
            anim.SetTrigger(clip.animatorTrigger);

        bool startedInAir = !ctrl.Grounded;
        bool landed = false;

        // --- STARTUP ---
        for (int i = 0; i < clip.startupFrames; i++)
        {
            if (startedInAir && ctrl.Grounded && aerialEndsOnLanding)
            {
                landed = true;
                break;
            }
            yield return new WaitForFixedUpdate();
        }

        if (landed)
        {
            HandleLandingLag(clip);
            busy = false;
            yield break;
        }

        // --- ACTIVE ---
        int maxActive = 0;
        foreach (var w in clip.windows)
            maxActive = Mathf.Max(maxActive, w.startFrame + w.activeFrames - 1);

        var hitThisAttack = new HashSet<PlatformFighterActor>();

        for (int f = 1; f <= maxActive; f++)
        {
            if (startedInAir && ctrl.Grounded && aerialEndsOnLanding)
            {
                landed = true;
                break;
            }

            foreach (var w in clip.windows)
            {
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
                        if (t == null || t == actor) continue;
                        if (hitThisAttack.Contains(t)) continue;

                        t.AddDamage(w.damage);
                        Vector2 kb = new Vector2(w.kbDir.x * facing, w.kbDir.y);
                        t.ApplyKnockback(kb, w.baseKB, w.growth);
                        hitThisAttack.Add(t);
                    }

                    if (showHitVis) DrawCircle(center, r, Color.red, circleSegments);
                }
                else
                {
                    var hits = Physics2D.OverlapBoxAll(center, w.size, 0f, hurtboxMask);
                    foreach (var h in hits)
                    {
                        var t = h.GetComponentInParent<PlatformFighterActor>();
                        if (t == null || t == actor) continue;
                        if (hitThisAttack.Contains(t)) continue;

                        t.AddDamage(w.damage);
                        Vector2 kb = new Vector2(w.kbDir.x * facing, w.kbDir.y);
                        t.ApplyKnockback(kb, w.baseKB, w.growth);
                        hitThisAttack.Add(t);
                    }

                    if (showHitVis)
                    {
                        Vector2 half = w.size * 0.5f;
                        Vector3 a = new(center.x - half.x, center.y - half.y);
                        Vector3 b = new(center.x - half.x, center.y + half.y);
                        Vector3 c = new(center.x + half.x, center.y + half.y);
                        Vector3 d = new(center.x + half.x, center.y - half.y);
                        Debug.DrawLine(a, b, Color.red, Time.fixedDeltaTime);
                        Debug.DrawLine(b, c, Color.red, Time.fixedDeltaTime);
                        Debug.DrawLine(c, d, Color.red, Time.fixedDeltaTime);
                        Debug.DrawLine(d, a, Color.red, Time.fixedDeltaTime);
                    }
                }
            }

            yield return new WaitForFixedUpdate();
        }

        if (landed)
        {
            HandleLandingLag(clip);
            busy = false;
            yield break;
        }

        // --- ENDLAG (air only) ---
        for (int i = 0; i < clip.endlagFrames; i++)
        {
            if (startedInAir && ctrl.Grounded && aerialEndsOnLanding)
            {
                HandleLandingLag(clip);
                busy = false;
                yield break;
            }
            yield return new WaitForFixedUpdate();
        }

        busy = false;
    }

    void HandleLandingLag(AttackClip clip)
    {
        int lag = Mathf.Max(0, clip.landingLag);
        if (lag <= 0) return;

        if (anim)
        {
            anim.ResetTrigger(clip.animatorTrigger);
            anim.SetTrigger("Land");
        }

        ctrl.BeginLandingLag(lag);
        if (tint) tint.SetLandingLagTint(true);

        StartCoroutine(LandingLagRoutine(lag));
    }

    IEnumerator LandingLagRoutine(int frames)
    {
        for (int i = 0; i < frames; i++)
            yield return new WaitForFixedUpdate();

        if (anim) anim.SetTrigger("LandingDone");
        if (tint) tint.SetLandingLagTint(false);
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
