using UnityEngine;
using UnityEngine.InputSystem;

/// Central input reader for platform fighter characters.
/// Connect this to Unity's PlayerInput component using "Invoke Unity Events".
/// Each action in your Input Actions asset (Move, Jump, Attack, etc.)
/// should call the matching method below.
/// Other scripts (movement, attacks) just read the public properties.
public class FighterInput : MonoBehaviour
{
    [Header("Tuning")]
    [Tooltip("Stick deflection threshold for counting as Smash input if no modifier held.")]
    public float smashStickThreshold = 0.75f;
    [Tooltip("Minimum stick deflection to register as Up/Down/Side for move selection.")]
    public float dirThreshold = 0.5f;

    // Movement vector from input
    public Vector2 Move { get; private set; }

    // One-frame "pressed this frame" flags
    public bool JumpPressed { get; private set; }
    public bool AttackPressed { get; private set; }
    public bool SpecialPressed { get; private set; }
    public bool GrabPressed { get; private set; }
    public bool SmashPressed { get; private set; }
    public bool RunTogglePressed { get; private set; }

    // Held states (true while button is held down)
    public bool JumpHeld { get; private set; }     // <-- NEW: properly implemented
    public bool AttackHeld { get; private set; }
    public bool ShieldHeld { get; private set; }
    public bool SmashHeld { get; private set; }

    // Reset one-frame flags each LateUpdate
    void LateUpdate()
    {
        JumpPressed = false;
        AttackPressed = false;
        SpecialPressed = false;
        GrabPressed = false;
        SmashPressed = false;
        RunTogglePressed = false;
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Move = ctx.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        JumpHeld = !ctx.canceled;      // true while button is held
        if (ctx.started)
            JumpPressed = true;        // true for one frame when first pressed
    }

    public void OnAttack(InputAction.CallbackContext ctx)
    {
        AttackHeld = !ctx.canceled;
        if (ctx.started)
            AttackPressed = true;
        Debug.Log("Attack Pressed");
    }

    public void OnSpecial(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
            SpecialPressed = true;
        Debug.Log("Special Pressed");
    }

    public void OnShield(InputAction.CallbackContext ctx)
    {
        ShieldHeld = !ctx.canceled;
    }

    public void OnGrab(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
            GrabPressed = true;
    }

    public void OnSmashModifier(InputAction.CallbackContext ctx)
    {
        SmashHeld = !ctx.canceled;
    }

    public void OnSmash(InputAction.CallbackContext ctx)
    {
        SmashHeld = !ctx.canceled;
        if (ctx.started) SmashPressed = true;
        Debug.Log("Smash Pressed");
    }

    public void OnRun(InputAction.CallbackContext ctx)
    {
        // Toggle on *press* only
        if (ctx.started) RunTogglePressed = true;
    }
}
