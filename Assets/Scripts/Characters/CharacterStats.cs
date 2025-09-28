using UnityEngine;

[CreateAssetMenu(menuName = "SSB/Character Stats")]
public class CharacterStats : ScriptableObject
{
    [Header("Movement")]
    public float walkSpeed = 3.5f;
    public float airSpeed = 2.8f;

    [Header("Jumping")]
    public float jumpForce = 8f;
    public float doubleJumpForce = 7f;
    public float shortHopForce = 5f; // if jump released early
    public int jumpSquatFrames = 4; // frames before jump takes off
    public int maxJumps = 2;

    [Header("Run / Dash")]
    public float dashSpeed = 8f;          // initial dash horizontal speed
    public int   dashFrames = 10;         // duration of initial dash in frames (60fps)
    public float runSpeed = 6.5f;         // top speed while running
    public float runAccel = 40f;          // accel toward runSpeed
    public float runDecel = 50f;          // decel when releasing run

    [Header("Combat")]
    public float weight = 100f; // higher = less knockback
    public float gravityScale = 3.5f;
}
