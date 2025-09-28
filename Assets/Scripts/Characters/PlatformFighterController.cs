using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(FighterInput))]
public class PlatformFighterController : MonoBehaviour
{
    [Header("Stats")]
    public CharacterStats stats;   // Holds: gravityScale, maxJumps, walkSpeed, airSpeed,
                                   // jumpForce, doubleJumpForce, shortHopForce,
                                   // dashSpeed, dashFrames, runSpeed, runAccel, runDecel,
                                   // jumpSquatFrames

    [Header("Ground Check (choose one)")]
    [Tooltip("Optional. If assigned, a small circle probe is used here.")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.06f;
    [Tooltip("Layers considered ground.")]
    public LayerMask groundMask;

    [Header("Auto Probe (if groundCheck is not assigned)")]
    [Tooltip("Extra thickness added under the collider for the box probe.")]
    public float probeSkin = 0.04f;
    [Tooltip("How wide the auto probe is relative to the collider (0–1).")]
    [Range(0.5f, 1.2f)] public float probeWidthMultiplier = 0.9f;

    // --- Running state ---
    public enum MoveState { Idle, Walk, InitialDash, Run }
    public MoveState moveState { get; private set; } = MoveState.Idle;
    bool runMode;
    int dashFramesLeft;

    // Components
    FighterInput input;
    Rigidbody2D rb;
    Collider2D col;
    Animator anim;

    // Status
    public bool FacingRight { get; private set; } = true;
    public bool Grounded { get; private set; }
    int jumpsLeft;

    // Jump squat / short hop
    int  jumpSquatCounter = 0;
    bool shortHopQueued   = false;

    // =========================
    //   MOMENTUM / CARRY
    // =========================

    [Header("Momentum Carry (optional nudge after takeoff)")]
    [Tooltip("Scales the pre-jump horizontal speed for the snapshot at takeoff.")]
    [Range(0f, 1.25f)] public float inheritFactor = 1.0f;

    [Tooltip("Blend some of the pre-jump speed for a short window after takeoff.")]
    public bool useDecayingCarry = false;

    [Tooltip("Seconds to keep blending pre-jump speed (when enabled).")]
    public float carryDuration = 0.12f;

    [Tooltip("Remaining fraction at the end of the carry window.")]
    [Range(0f, 1f)] public float carryEndFactor = 0.0f;

    [Tooltip("Clamp the initial inherited snapshot to Air Speed on takeoff.")]
    public bool clampInheritedToAirMax = false;
    
    [Tooltip("Extra seconds to keep carry active AFTER apex before handing back to air control.")]
    public float carryAfterApexTime = 0.10f;
    float preJumpSpeedX;
    float carryTimer;
    bool  inCarryWindow;
    float carryApexTimer;

    // ---- Melee-style Jump Carry (hard preserve during rise) ----
    [Header("Jump Carry Window (Melee-like)")]
    [Tooltip("Preserve pre-jump X for the whole rise; give control back on apex or timeout.")]
    public bool preserveRunSpeedDuringJump = true;

    [Tooltip("Release the carry when vertical velocity <= 0 (apex).")]
    public bool releaseCarryOnApex = true;

    [Tooltip("Guarantee at least this much time (s) of preserved X after takeoff.")]
    public float carryMinTime = 0.04f;   // ~2–3 frames @ 60fps

    [Tooltip("Fail-safe timeout (s); hand back control if apex not reached yet.")]
    public float carryMaxTime = 0.22f;

    [Tooltip("Cap preserved X during the jump to this. If <= 0, uses stats.runSpeed.")]
    public float jumpCarryMaxX = 0f;

    bool  jumpCarryActive;
    float jumpCarryT;
    float jumpCarryX; // preserved X while rising

    // =========================
    //        Facing
    // =========================
    [Header("Facing")]
    [Tooltip("Allow turning while airborne. Leave OFF for 'no mid-air flips'.")]
    public bool allowAirFacingFlips = false;
    [Tooltip("On ground, if no input then face by velocity.")]
    public bool faceByVelocityWhenIdle = true;

    // =========================
    //   Landing Transfer
    // =========================
    [Header("Landing Transfer")]
    public bool enableLandingTransfer = true;
    public enum LandingMode { ClampOnly, BlendToTarget }
    public LandingMode landingMode = LandingMode.ClampOnly;

    [Tooltip("How long to blend into ground target after landing (BlendToTarget).")]
    public float landingBlendTime = 0.08f;

    [Tooltip("Clamp X speed to run max on landing.")]
    public bool landingClampToGroundMax = true;

    bool  landingBlending;
    float landingBlendT;
    float landingStartVX;

    // =========================
    //     Air Control
    // =========================
    [Header("Air Control (local)")]
    public float airAccel = 35f;
    public float airDecel = 35f;

    // ------------------------------------------------------------

    void Awake()
    {
        input = GetComponent<FighterInput>();
        rb    = GetComponent<Rigidbody2D>();
        col   = GetComponent<Collider2D>();
        anim  = GetComponentInChildren<Animator>();

        if (stats != null) rb.gravityScale = stats.gravityScale;
        jumpsLeft = stats != null ? stats.maxJumps : 1;
    }

    void FlipToFacing()
    {
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (FacingRight ? 1 : -1);
        transform.localScale = s;
    }

    void Update()
    {
        // Ground check + landing transition
        bool wasGrounded = Grounded;
        Grounded = DoGroundCheck();

        if (!wasGrounded && Grounded)
        {
            // Landed
            jumpsLeft        = stats.maxJumps;
            jumpSquatCounter = 0;
            shortHopQueued   = false;
            OnLanded();
        }

        // Facing (no mid-air flips unless allowed)
        if ((Grounded || allowAirFacingFlips) && Mathf.Abs(input.Move.x) > 0.01f)
        {
            bool shouldFaceRight = input.Move.x > 0f;
            if (shouldFaceRight != FacingRight)
            {
                FacingRight = shouldFaceRight;
                FlipToFacing();
            }
        }
        else if (Grounded && faceByVelocityWhenIdle)
        {
            float vx = rb.linearVelocity.x;
            if (Mathf.Abs(vx) > 0.05f)
            {
                bool shouldFaceRight = vx > 0f;
                if (shouldFaceRight != FacingRight)
                {
                    FacingRight = shouldFaceRight;
                    FlipToFacing();
                }
            }
        }

        // Run toggle
        if (input.RunTogglePressed)
        {
            runMode = !runMode;

            if (runMode)
            {
                if (Grounded && (moveState == MoveState.Idle || moveState == MoveState.Walk) &&
                    Mathf.Abs(input.Move.x) > 0.1f)
                {
                    moveState = MoveState.InitialDash;
                    dashFramesLeft = Mathf.Max(1, stats.dashFrames);
                    anim?.SetTrigger("dash_start");
                }
            }
            else if (moveState == MoveState.InitialDash)
            {
                moveState = MoveState.Idle;
            }
        }

        // Kick off dash later if stick is tilted after toggling run
        if (runMode && Grounded && (moveState == MoveState.Idle || moveState == MoveState.Walk) &&
            Mathf.Abs(input.Move.x) > 0.1f)
        {
            moveState = MoveState.InitialDash;
            dashFramesLeft = Mathf.Max(1, stats.dashFrames);
            anim?.SetTrigger("dash_start");
        }

        // Jump logic
        if (Grounded)
        {
            if (input.JumpPressed && jumpSquatCounter == 0)
            {
                jumpSquatCounter = Mathf.Max(0, stats.jumpSquatFrames);
                shortHopQueued = false;
                if (jumpSquatCounter > 0) anim?.SetTrigger("JumpSquat");
            }

            if (jumpSquatCounter > 0 && !input.JumpHeld)
                shortHopQueued = true;
        }
        else
        {
            // Airborne: double jump
            if (input.JumpPressed && jumpsLeft > 0 && jumpSquatCounter == 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                rb.AddForce(Vector2.up * stats.doubleJumpForce, ForceMode2D.Impulse);
                jumpsLeft--;

                ApplyCarry(); // carry for double jump, too
            }
        }

        // Decaying carry timer
        if (inCarryWindow)
        {
            carryTimer += Time.deltaTime;
            if (carryTimer >= carryDuration) inCarryWindow = false;
        }

        // Animator (optional)
        if (anim)
        {
            anim.SetBool("grounded", Grounded);
            anim.SetFloat("speed", Mathf.Abs(rb.linearVelocity.x));
            anim.SetBool("running", moveState == MoveState.Run || moveState == MoveState.InitialDash);
        }
    }

    void FixedUpdate()
    {
        // Resolve jump squat on physics step
        if (jumpSquatCounter > 0)
        {
            jumpSquatCounter--;
            if (jumpSquatCounter == 0)
            {
                // Launch (first jump)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                float jf = shortHopQueued ? stats.shortHopForce : stats.jumpForce;
                rb.AddForce(Vector2.up * jf, ForceMode2D.Impulse);
                jumpsLeft--;

                ApplyCarry();
            }
        }

        Vector2 v = rb.linearVelocity;

        if (!Grounded)
        {
            // --- Jump-carry lock: preserve pre-jump X while rising ---
            if (jumpCarryActive)
            {
                v.x = jumpCarryX; // exact preserve

                // release conditions
                jumpCarryT += Time.fixedDeltaTime;
                bool pastMin = jumpCarryT >= carryMinTime;
                bool hitApex = rb.linearVelocity.y <= 0f;

            if (releaseCarryOnApex && pastMin)
            {
                if (hitApex)
                {
                    // start counting how long past apex we’ve lingered
                    carryApexTimer += Time.fixedDeltaTime;
                    if (carryApexTimer >= carryAfterApexTime)
                    {
                        jumpCarryActive = false;
                        v.x = Mathf.Clamp(v.x, -stats.airSpeed, stats.airSpeed);
                    }
                }
            }
            else if (jumpCarryT >= carryMaxTime)
            {
                // fail-safe timeout
                jumpCarryActive = false;
                v.x = Mathf.Clamp(v.x, -stats.airSpeed, stats.airSpeed);
            }
            }
            else
            {
                // --- Normal air control after carry is released ---
                float desired = input.Move.x * stats.airSpeed;
                float aa = (Mathf.Abs(desired) > 0.01f) ? airAccel : airDecel;
                v.x = Mathf.MoveTowards(v.x, desired, aa * Time.fixedDeltaTime);

                // Optional decaying carry nudge
                if (useDecayingCarry && inCarryWindow)
                {
                    float t = Mathf.Clamp01(carryTimer / Mathf.Max(0.0001f, carryDuration));
                    float carryBlend = Mathf.Lerp(inheritFactor, carryEndFactor, t);
                    float carryTarget = preJumpSpeedX * carryBlend;
                    v.x = Mathf.MoveTowards(v.x, carryTarget, aa * 0.5f * Time.fixedDeltaTime);
                }

                // Post-carry soft cap by air speed
                v.x = Mathf.Clamp(v.x, -stats.airSpeed, stats.airSpeed);
            }
        }
        else
        {
            // Ground movement
            switch (moveState)
            {
                case MoveState.InitialDash:
                {
                    float dir = FacingRight ? 1f : -1f;
                    v.x = stats.dashSpeed * dir;
                    dashFramesLeft--;
                    if (dashFramesLeft <= 0) moveState = MoveState.Run;
                    break;
                }

                case MoveState.Run:
                {
                    float want = (runMode && Mathf.Abs(input.Move.x) > 0.1f)
                        ? Mathf.Sign(input.Move.x) * stats.runSpeed
                        : 0f;

                    float accel = (Mathf.Abs(want) > 0.01f) ? stats.runAccel : stats.runDecel;
                    v.x = Mathf.MoveTowards(v.x, want, accel * Time.fixedDeltaTime);

                    if (Mathf.Abs(v.x) < 0.02f && want == 0f) moveState = MoveState.Idle;
                    break;
                }

                default:
                {
                    float target = input.Move.x * stats.walkSpeed;
                    v.x = target;
                    moveState = Mathf.Abs(target) > 0.05f ? MoveState.Walk : MoveState.Idle;
                    break;
                }
            }

            // Landing blend (optional butter)
            if (enableLandingTransfer && landingMode == LandingMode.BlendToTarget && landingBlending)
            {
                landingBlendT += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(landingBlendT / Mathf.Max(0.0001f, landingBlendTime));
                float groundTarget = input.Move.x * stats.walkSpeed;
                float blended = Mathf.Lerp(landingStartVX, groundTarget, t);
                v.x = Mathf.MoveTowards(blended, groundTarget, stats.runAccel * Time.fixedDeltaTime);
                if (t >= 1f) landingBlending = false;
            }
        }

        rb.linearVelocity = new Vector2(v.x, rb.linearVelocity.y);
    }

    // Called on takeoff (ground jump and double jump)
    void ApplyCarry()
    {
        preJumpSpeedX = rb.linearVelocity.x;

        // Snapshot inheritance (pre-jump ground X)
        float targetX = preJumpSpeedX * inheritFactor;

        // Start jump-carry window (preserve X while rising)
        if (preserveRunSpeedDuringJump)
        {
            float cap = (jumpCarryMaxX > 0f) ? jumpCarryMaxX : stats.runSpeed;
            jumpCarryX = Mathf.Clamp(targetX, -cap, cap);
            jumpCarryActive = true;
            jumpCarryT = 0f;
            carryApexTimer = 0f;
        }

        // Optional decaying carry window (additional nudge)
        if (useDecayingCarry && carryDuration > 0f)
        {
            carryTimer = 0f;
            inCarryWindow = true;
        }
        else inCarryWindow = false;

        // Optional clamp at the snapshot moment
        if (clampInheritedToAirMax)
            targetX = Mathf.Clamp(targetX, -stats.airSpeed, stats.airSpeed);

        // Apply snapshot now
        rb.linearVelocity = new Vector2(targetX, rb.linearVelocity.y);

        // Stop any landing blend once airborne
        landingBlending = false;
    }

    void OnLanded()
    {
        // End any jump-carry
        jumpCarryActive = false;

        if (!enableLandingTransfer) return;

        if (landingClampToGroundMax)
        {
            float clamped = Mathf.Clamp(rb.linearVelocity.x, -stats.runSpeed, stats.runSpeed);
            rb.linearVelocity = new Vector2(clamped, rb.linearVelocity.y);
        }

        if (landingMode == LandingMode.BlendToTarget)
        {
            landingStartVX = rb.linearVelocity.x;
            landingBlendT = 0f;
            landingBlending = landingBlendTime > 0f;
        }
    }

    // ---------------- Grounding ----------------
    bool DoGroundCheck()
    {
        if (groundCheck != null)
            return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);

        Bounds b = col.bounds;
        float width  = b.size.x * probeWidthMultiplier;
        float height = probeSkin; // very thin
        Vector2 size   = new Vector2(width, height);
        Vector2 center = new Vector2(b.center.x, b.min.y - height * 0.5f);

        return Physics2D.OverlapBox(center, size, 0f, groundMask);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        if (groundCheck != null)
        {
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        else if (col != null)
        {
            Bounds b = col.bounds;
            float width  = b.size.x * probeWidthMultiplier;
            float height = probeSkin;
            Vector2 size   = new Vector2(width, height);
            Vector2 center = new Vector2(b.center.x, b.min.y - height * 0.5f);
            Gizmos.DrawWireCube(center, size);
        }
    }
#endif
}
