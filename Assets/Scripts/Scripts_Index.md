# Project Script Index

_Generated: 2025-10-04 07:02_

This index lists each script with a one-line purpose and quick references to classes, fields, methods, and Unity messages. Use this as a map across chats.

## `CharacterStats.cs`
- **Path:** `Scripts/Characters/CharacterStats.cs`
- **Classes:** CharacterStats
- **Purpose:** General gameplay behaviour
- **Key Fields:** `float walkSpeed`, `float airSpeed`, `float jumpForce`, `float doubleJumpForce`, `float shortHopForce`, `int jumpSquatFrames`

## `PlatformFighterAnimatorBridge.cs`
- **Path:** `Scripts/Characters/PlatformFighterAnimatorBridge.cs`
- **Classes:** PlatformFighterAnimatorBridge
- **Purpose:** General gameplay behaviour
- **Key Fields:** `Rigidbody2D rb`, `PlatformFighterController ctrl`, `Animator anim`, ` rb`, ` ctrl`, ` anim`
- **Unity Messages:** `Awake()`, `Update()`

## `PlatformFighterController.cs`
- **Path:** `Scripts/Characters/PlatformFighterController.cs`
- **Classes:** PlatformFighterController
- **Purpose:** Movement / physics
- **Key Fields:** `CharacterStats stats`, `Transform groundCheck`, `float groundCheckRadius`, `LayerMask groundMask`, `float probeSkin`, `bool runMode`
- **Properties:** `MoveState moveState`, `bool FacingRight`, `bool Grounded`
- **Methods:** `void FlipToFacing()`, ` if(!wasGrounded && Grounded)`, ` if(shouldFaceRight != FacingRight)`, `else if(Grounded && faceByVelocityWhenIdle)`, ` if(shouldFaceRight != FacingRight)`, ` if(input.RunTogglePressed)`, ` if(runMode)`, `else if(moveState == MoveState.InitialDash)`
- **Unity Messages:** `Awake()`, `Update()`, `FixedUpdate()`, `OnDrawGizmosSelected()`

## `AttackClip.cs`
- **Path:** `Scripts/Combat/AttackClip.cs`
- **Classes:** AttackClip, HitboxWindow
- **Purpose:** Combat / hit detection
- **Key Fields:** `int startupFrames`, `int endlagFrames`, `int landingLag`, `string animatorTrigger`, `List<HitboxWindow> windows`, `int startFrame`

## `AttackController.cs`
- **Path:** `Scripts/Combat/AttackController.cs`
- **Classes:** AttackController
- **Purpose:** Movement / physics; Combat / hit detection
- **Key Fields:** `AttackSet moves`, `LayerMask hurtboxMask`, `bool showHitVis`, `KeyCode toggleHitVisKey`, `bool useHitBubbles`, `bool bubbleRadiusFromMaxAxis`
- **Methods:** ` if(moves == null)`, ` if(input.SmashPressed)`, ` if(input.SpecialPressed)`, ` if(input.AttackPressed)`, `AttackClip SelectNormal()`, ` if(!ctrl.Grounded)`, `AttackClip SelectSmash()`, ` if(!ctrl.Grounded)`
- **Unity Messages:** `Awake()`, `Update()`

## `AttackDefinition.cs`
- **Path:** `Scripts/Combat/AttackDefinition.cs`
- **Classes:** AttackDefinition
- **Purpose:** Combat / hit detection
- **Key Fields:** `float startupFrames`, `float activeFrames`, `float endlagFrames`, `float damage`, `float baseKB`, `float growth`

## `AttackSet.cs`
- **Path:** `Scripts/Combat/AttackSet.cs`
- **Classes:** AttackSet
- **Purpose:** Combat / hit detection
- **Key Fields:** `AttackClip tilt`, `AttackClip upTilt`, `AttackClip downTilt`, `AttackClip nair`, `AttackClip fair`, `AttackClip bair`

## `HitBubble.cs`
- **Path:** `Scripts/Combat/HitBubble.cs`
- **Classes:** HitBubble
- **Purpose:** Combat / hit detection
- **Key Fields:** `float radius`, `Color activeColor`, `Color inactiveColor`, `bool active`
- **Unity Messages:** `OnDrawGizmos()`

## `Hurtbox.cs`
- **Path:** `Scripts/Combat/Hurtbox.cs`
- **Classes:** Hurtbox
- **Purpose:** Combat / hit detection
- **Key Fields:** `PlatformFighterActor owner`, `var c`
- **Unity Messages:** `Reset()`

## `PlatformFighterActor.cs`
- **Path:** `Scripts/Combat/PlatformFighterActor.cs`
- **Classes:** PlatformFighterActor
- **Purpose:** Movement / physics
- **Key Fields:** `Rigidbody2D rb`, ` dir`, `float k`
- **Properties:** `float percent`
- **Methods:** `void AddDamage(float amount)`, `void ApplyKnockback(Vector2 dir, float baseKB, float growth)`, `void ResetPercent()`
- **Unity Messages:** `Awake()`

## `CharacterTints.cs`
- **Path:** `Scripts/Core/CharacterTints.cs`
- **Classes:** CharacterTint
- **Purpose:** General gameplay behaviour
- **Key Fields:** `Color landingLagTint`, `SpriteRenderer[] renderers`, `Color[] originalColors`, ` renderers`, ` originalColors`
- **Methods:** `void SetLandingLagTint(bool enabled)`, ` if(enabled)`
- **Unity Messages:** `Awake()`

## `FPSCounter.cs`
- **Path:** `Scripts/Core/FPSCounter.cs`
- **Classes:** FPSCounter
- **Purpose:** General gameplay behaviour
- **Key Fields:** `float deltaTime`, `int w`, `GUIStyle style`, `Rect rect`, `float msec`, `float fps`
- **Methods:** `void OnGUI()`
- **Unity Messages:** `Update()`

## `FrameStepper.cs`
- **Path:** `Scripts/Core/FrameStepper.cs`
- **Classes:** TrainingModeController
- **Purpose:** Movement / physics
- **Key Fields:** `KeyCode pauseKey`, `KeyCode stepKey`, `KeyCode slowmoOnKey`, `KeyCode slowmoOffKey`, `KeyCode fps1ToggleKey`, `float slowmoScale`
- **Methods:** ` if(oneFpsMode)`, `IEnumerator StepOneFrame()`
- **Unity Messages:** `Update()`

## `GameSetup.cs`
- **Path:** `Scripts/Core/GameSetup.cs`
- **Classes:** GameSetup
- **Purpose:** General gameplay behaviour
- **Unity Messages:** `Awake()`

## `HitVisDrawer.cs`
- **Path:** `Scripts/Core/HitVisDrawer.cs`
- **Classes:** HitVisDrawer
- **Purpose:** Combat / hit detection
- **Key Fields:** `Material lineMaterial`, `bool visible`, `List<LineRenderer> pool`, `int usedThisFrame`, ` Instance`, `var shader`
- **Properties:** `HitVisDrawer Instance`
- **Methods:** ` if(Instance && Instance != this)`, ` if(!lineMaterial)`, `LineRenderer GetLR()`, ` if(usedThisFrame < pool.Count)`, `void ToggleVisible()`, `void DrawCircle(Vector2 center, float radius, Color color, int? segOverride = null)`, ` for(int i = 0; i < segs; i++)`, `void DrawBox(Vector2 center, Vector2 size, Color color)`
- **Unity Messages:** `Awake()`, `LateUpdate()`

## `FighterInput.cs`
- **Path:** `Scripts/Inputs/FighterInput.cs`
- **Classes:** FighterInput
- **Purpose:** Input reader / input state
- **Key Fields:** `float smashStickThreshold`, `float dirThreshold`, ` JumpPressed`, ` AttackPressed`, ` SpecialPressed`, ` GrabPressed`
- **Properties:** `Vector2 Move`, `bool JumpPressed`, `bool AttackPressed`, `bool SpecialPressed`, `bool GrabPressed`, `bool SmashPressed`
- **Methods:** `void OnMove(InputAction.CallbackContext ctx)`, `void OnJump(InputAction.CallbackContext ctx)`, `void OnAttack(InputAction.CallbackContext ctx)`, `void OnSpecial(InputAction.CallbackContext ctx)`, `void OnShield(InputAction.CallbackContext ctx)`, `void OnGrab(InputAction.CallbackContext ctx)`, `void OnSmashModifier(InputAction.CallbackContext ctx)`, `void OnSmash(InputAction.CallbackContext ctx)`
- **Unity Messages:** `LateUpdate()`

## `PercentUI.cs`
- **Path:** `Scripts/UI/PercentUI.cs`
- **Classes:** PercentUI
- **Purpose:** UI / HUD
- **Key Fields:** `PlatformFighterActor target`, `TextMeshProUGUI label`
- **Unity Messages:** `Update()`
