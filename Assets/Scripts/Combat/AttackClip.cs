using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SSB/Attack Clip")]
public class AttackClip : ScriptableObject
{
    [Header("Frame Timings")]
    public int startupFrames = 3;   // frames before first hitbox
    public int endlagFrames = 10;   // frames after last window
    public int landingLag = 0;      // applied if aerial lands during active

    [Header("Optional animation trigger")]
    public string animatorTrigger;  // e.g., "Jab", "UTilt", "Nair"

    [Header("Windows (sweetspot/late, multihit, etc.)")]
    public List<HitboxWindow> windows = new List<HitboxWindow>(); // processed in order
}

public enum HitKind { Single, Multi }

[Serializable]
public class HitboxWindow
{
    // -------- Timing --------
    [Header("Timing (relative to after startup)")]
    [Tooltip("First active frame (1-based).")]
    public int startFrame = 1;
    [Tooltip("Duration this window is active (frames).")]
    public int activeFrames = 2;

    // -------- Shape --------
    [Header("Hitbox shape")]
    [Tooltip("Local space; X flips by facing.")]
    public Vector2 offset = new Vector2(0.2f, 0.1f);
    public Vector2 size   = new Vector2(0.30f, 0.30f);

    // -------- On-hit numbers --------
    [Header("On-hit")]
    public float damage = 8f;

    [Tooltip("Base knockback scalar sent to ApplyKnockback.")]
    public float baseKB = 3.0f;

    [Tooltip("Knockback growth scalar sent to ApplyKnockback.")]
    public float growth = 0.06f;

    // --- Legacy XY direction (kept for backward-compat) ---
    [Tooltip("Legacy XY direction (unit-ish). X flips by facing. Only used if 'useKbAngle' & 'useSakuraiAngle' are both OFF.")]
    public Vector2 kbDir = new Vector2(1, 0.4f);

    // -------- Degrees-based knockback --------
    [Header("KB (Degrees Mode)")]
    [Tooltip("If ON, uses kbAngleDeg (degrees) instead of kbDir XY (unless Sakurai is ON).")]
    public bool useKbAngle = true;

    [Tooltip("Knockback angle in DEGREES. 0 = forward, 90 = up, -90 = down.")]
    [Range(-179f, 179f)]
    public float kbAngleDeg = 45f;

    [Tooltip("If true, 0° points toward character facing. If false, 0° is world +X.")]
    public bool angleIsRelativeToFacing = true;

    // -------- Sakurai angle (361° behaviour) --------
    [Header("Sakurai Angle")]
    [Tooltip("If ON, uses Sakurai-style angle: grounded/airborne angles below.")]
    public bool useSakuraiAngle = false;

    [Tooltip("Target grounded => this angle (deg). Default 0° like Smash.")]
    [Range(-179f, 179f)]
    public float sakuraiGroundAngleDeg = 0f;

    [Tooltip("Target airborne => this angle (deg). Default 45° like Smash.")]
    [Range(-179f, 179f)]
    public float sakuraiAirAngleDeg = 45f;

    // -------- Ground clamp helpers --------
    [Header("Ground Clamp")]
    [Tooltip("When target is grounded, ensure at least this many degrees upward (prevents 'sticking' to floor). 0 keeps pure angle.")]
    [Range(0f, 15f)]
    public float minUpDegreesOnGround = 0f;

    // -------- Hit behaviour --------
    [Header("Hit Behaviour")]
    public HitKind hitKind = HitKind.Single;

    [Tooltip("If true (typical sweetspot), once this window hits, later windows in this attack won't hit.")]
    public bool consumeAttackOnHit = true;

    [Tooltip("Seconds between hits per target for Multi windows (ignored for Single).")]
    public float multiHitInterval = 0.10f;
}
