using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(FighterInput))]
public class PlatformFighterController : MonoBehaviour
{
    // ---------- Landing Lag (NEW) ----------
    [Header("Landing Lag")]
    [Tooltip("Animator Trigger that plays your Landing animation.")]
    public string landingAnimTrigger = "Land";
    [Tooltip("Animator Trigger that plays when landing lag is done.")]
    public string landingDoneTrigger = "LandingDone";

    int landingLagFramesLeft = 0;
    public bool InLandingLag => landingLagFramesLeft > 0;

    CharacterTint tint; // optional tint during lag
    // ---------------------------------------

    [Header("Stats")]
    public CharacterStats stats;

    [Header("Ground Check (choose one)")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.06f;
    public LayerMask groundMask;

    [Header("Auto Probe (if groundCheck is not assigned)")]
    public float probeSkin = 0.04f;
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
    PlatformFighterActor actor;

    int hardLagFrames = 0;
    public bool HardLocked => hardLagFrames > 0;

    public bool FacingRight { get; private set; } = true;
    public bool Grounded { get; private set; }
    int jumpsLeft;

    int jumpSquatCounter = 0;
    bool shortHopQueued = false;

    // =========================
    //   MOMENTUM / CARRY
    // =========================
    [Header("Momentum Carry (optional nudge after takeoff)")]
    [Range(0f, 1.25f)] public float inheritFactor = 1.0f;
    public bool useDecayingCarry = false;
    public float carryDuration = 0.12f;
    [Range(0f, 1f)] public float carryEndFactor = 0.0f;
    public bool clampInheritedToAirMax = false;
    public float carryAfterApexTime = 0.10f;
    float preJumpSpeedX;
    float carryTimer;
    bool inCarryWindow;
    float carryApexTimer;

    [Header("Jump Carry Window (Melee-like)")]
    public bool preserveRunSpeedDuringJump = true;
    public bool releaseCarryOnApex = true;
    public float carryMinTime = 0.04f;
    public float carryMaxTime = 0.22f;
    public float jumpCarryMaxX = 0f;

    bool jumpCarryActive;
    float jumpCarryT;
    float jumpCarryX;

    // =========================
    //        Facing
    // =========================
    [Header("Facing")]
    public bool allowAirFacingFlips = false;
    public bool faceByVelocityWhenIdle = true;

    // =========================
    //   Landing Transfer
    // =========================
    [Header("Landing Transfer")]
    public bool enableLandingTransfer = true;
    public enum LandingMode { ClampOnly, BlendToTarget }
    public LandingMode landingMode = LandingMode.ClampOnly;
    public float landingBlendTime = 0.08f;
    public bool landingClampToGroundMax = true;

    bool landingBlending;
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
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        anim = GetComponentInChildren<Animator>();
        actor = GetComponent<PlatformFighterActor>();

        tint = GetComponent<CharacterTint>();
        if (tint) tint.SetLandingLagTint(false);

        if (stats != null) rb.gravityScale = stats.gravityScale;
        jumpsLeft = stats != null ? stats.maxJumps : 1;
    }

    // Called by AttackController
    public void BeginLandingLag(int frames)
    {
        if (frames <= 0) return;

        landingLagFramesLeft = frames;
        jumpSquatCounter = 0;
        shortHopQueued = false;
        jumpCarryActive = false;

        if (anim && !string.IsNullOrEmpty(landingAnimTrigger))
            anim.SetTrigger(landingAnimTrigger);

        if (tint) tint.SetLandingLagTint(true);
    }

    public void StartHardLock(int frames)
    {
        if (frames <= 0) return;
        hardLagFrames = Mathf.Max(hardLagFrames, frames);
    }

    void FlipToFacing()
    {
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (FacingRight ? 1 : -1);
        transform.localScale = s;
    }

    void Update()
    {
        // --- HARD LOCK freeze ---
        if (HardLocked)
        {
            if (hardLagFrames > 0) hardLagFrames--;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (hardLagFrames <= 0 && tint) tint.SetLandingLagTint(false);
            return;
        }

        // --- GROUND CHECK ---
        bool wasGrounded = Grounded;
        Grounded = DoGroundCheck();

        if (!wasGrounded && Grounded)
        {
            jumpsLeft = stats.maxJumps;
            jumpSquatCounter = 0;
            shortHopQueued = false;
            OnLanded();
        }

        // --- LANDING LAG lock ---
        if (InLandingLag)
        {
            if (anim)
            {
                anim.SetBool("grounded", Grounded);
                anim.SetFloat("speed", Mathf.Abs(rb.linearVelocity.x));
            }
            return;
        }

        // Facing
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

        // Late dash trigger
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
                ApplyCarry();
            }
        }

        // Carry timer
        if (inCarryWindow)
        {
            carryTimer += Time.deltaTime;
            if (carryTimer >= carryDuration) inCarryWindow = false;
        }

        if (anim)
        {
            anim.SetBool("grounded", Grounded);
            anim.SetFloat("speed", Mathf.Abs(rb.linearVelocity.x));
        }
    }

    void FixedUpdate()
    {
        // --- HARD LOCK freeze ---
        if (HardLocked)
        {
            if (hardLagFrames > 0) hardLagFrames--;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (hardLagFrames <= 0 && tint) tint.SetLandingLagTint(false);
            return;
        }

        // --- MOVEMENT LOCK during landing lag ---
        if (InLandingLag)
        {
            var vLock = rb.linearVelocity;
            vLock.x = 0f;
            rb.linearVelocity = new Vector2(vLock.x, rb.linearVelocity.y);

            landingLagFramesLeft--;
            if (landingLagFramesLeft <= 0)
            {
                if (tint) tint.SetLandingLagTint(false);
                if (anim && !string.IsNullOrEmpty(landingDoneTrigger))
                    anim.SetTrigger(landingDoneTrigger);
            }
            return;
        }

        // --- Jump squat ---
        if (jumpSquatCounter > 0)
        {
            jumpSquatCounter--;
            if (jumpSquatCounter == 0)
            {
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
            // --- Jump carry ---
            if (jumpCarryActive)
            {
                v.x = jumpCarryX;

                jumpCarryT += Time.fixedDeltaTime;
                bool pastMin = jumpCarryT >= carryMinTime;
                bool hitApex = rb.linearVelocity.y <= 0f;

                if (releaseCarryOnApex && pastMin)
                {
                    if (hitApex)
                    {
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
                    jumpCarryActive = false;
                    v.x = Mathf.Clamp(v.x, -stats.airSpeed, stats.airSpeed);
                }
            }
            else
            {
                float desired = input.Move.x * stats.airSpeed;
                float aa = (Mathf.Abs(desired) > 0.01f) ? airAccel : airDecel;
                v.x = Mathf.MoveTowards(v.x, desired, aa * Time.fixedDeltaTime);

                if (useDecayingCarry && inCarryWindow)
                {
                    float t = Mathf.Clamp01(carryTimer / Mathf.Max(0.0001f, carryDuration));
                    float carryBlend = Mathf.Lerp(inheritFactor, carryEndFactor, t);
                    float carryTarget = preJumpSpeedX * carryBlend;
                    v.x = Mathf.MoveTowards(v.x, carryTarget, aa * 0.5f * Time.fixedDeltaTime);
                }

                v.x = Mathf.Clamp(v.x, -stats.airSpeed, stats.airSpeed);
            }
        }
        else
        {
            // --- Ground movement ---
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

            // --- Landing blend (optional) ---
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

        if (actor == null || !actor.InHitstun)
            rb.linearVelocity = new Vector2(v.x, rb.linearVelocity.y);

        if (hardLagFrames > 0) hardLagFrames--;
    }

    // --- Carry helpers ---
    void ApplyCarry()
    {
        preJumpSpeedX = rb.linearVelocity.x;
        float targetX = preJumpSpeedX * inheritFactor;

        if (preserveRunSpeedDuringJump)
        {
            float cap = (jumpCarryMaxX > 0f) ? jumpCarryMaxX : stats.runSpeed;
            jumpCarryX = Mathf.Clamp(targetX, -cap, cap);
            jumpCarryActive = true;
            jumpCarryT = 0f;
            carryApexTimer = 0f;
        }

        if (useDecayingCarry && carryDuration > 0f)
        {
            carryTimer = 0f;
            inCarryWindow = true;
        }
        else inCarryWindow = false;

        if (clampInheritedToAirMax)
            targetX = Mathf.Clamp(targetX, -stats.airSpeed, stats.airSpeed);

        rb.linearVelocity = new Vector2(targetX, rb.linearVelocity.y);
        landingBlending = false;
    }

    void OnLanded()
    {
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

    bool DoGroundCheck()
    {
        if (groundCheck != null)
            return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);

        Bounds b = col.bounds;
        float width = b.size.x * probeWidthMultiplier;
        float height = probeSkin;
        Vector2 size = new Vector2(width, height);
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
            float width = b.size.x * probeWidthMultiplier;
            float height = probeSkin;
            Vector2 size = new Vector2(width, height);
            Vector2 center = new Vector2(b.center.x, b.min.y - height * 0.5f);
            Gizmos.DrawWireCube(center, size);
        }
    }
#endif
}
