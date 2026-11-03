using UnityEngine;
using UnityEngine.InputSystem;

namespace IronPulse
{
 public sealed partial class IPPlayer : MonoBehaviour
 {
  public Camera ViewCamera { get; private set; }
  public IPViewWeapon Weapon { get; private set; }
  public float Health { get; private set; } = 100f;
  public bool IsDead => Health <= 0f;
  public bool IsExecuting { get; private set; }
  public IPExecutionStyle CurrentExecutionStyle { get; private set; }
  public IPExecutionStyle LastExecutionStyle { get; private set; }
  public int ExecutionCount { get; private set; }
  public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;
  public int Shells => Weapon != null ? Weapon.LoadedShells : 2;
  public int MagazineCapacity => Weapon != null ? Weapon.Capacity : 2;
  public string WeaponDisplayName => Weapon != null ? Weapon.WeaponName : "DOUBLE BARREL";
  public float DashCooldown01 => Mathf.Clamp01(dashCooldown / DashRecovery);
  public int JumpsRemaining { get; private set; } = 2;
  public float DamageFlash { get; private set; }
  public float HitConfirm { get; private set; }
  public IPEnemy ExecutionCandidate { get; private set; }
  public bool IsGrounded => grounded;
  public bool IsDashing => dashRemaining > 0f;
  const float Speed = 11.4f, DashRecovery = .72f, EyeHeight = 1.58f;
  CharacterController controller;
  Vector3 horizontalVelocity, dashDirection, spawnPoint;
  float verticalVelocity, yaw, pitch, dashCooldown, dashRemaining, coyoteTime, jumpBuffer, hitProtection;
  float bobPhase, cameraRoll, landingKick, trauma, recoil, candidateTimer, damageLean, shotLateral, shotPush;
  bool initialized, grounded, wasGrounded;
  IPEnemy executionTarget;
  float executionTime, executionDuration, executionHitStop;
  Vector3 executionEyeStart;
  float executionFovStart;
  bool executionHit, executionGrabHit;
  Vector3 executionStart, executionEnd, executionFocus, executionRight, executionForward, executionCut;
  Quaternion executionLookStart;
  Vector2 mouseDelta;

  public void Initialize(Vector3 spawn)
  {
   if (initialized) { ResetAt(spawn); return; }
   initialized = true; gameObject.name = "HUMAN // IRON PULSE"; gameObject.layer = 2;
   spawnPoint = spawn; transform.position = spawn; yaw = transform.eulerAngles.y;
   controller = GetComponent<CharacterController>();
   if (controller == null) controller = gameObject.AddComponent<CharacterController>();
   controller.height = 1.8f; controller.radius = .31f; controller.center = Vector3.up * .9f;
   controller.stepOffset = .35f; controller.slopeLimit = 52f; controller.skinWidth = .035f; controller.minMoveDistance = 0f;
   var cameraObject = new GameObject("Human eye camera");
   cameraObject.transform.SetParent(transform, false); cameraObject.transform.localPosition = Vector3.up * EyeHeight; cameraObject.tag = "MainCamera";
   ViewCamera = cameraObject.AddComponent<Camera>(); ViewCamera.nearClipPlane = .035f; ViewCamera.farClipPlane = 250f; ViewCamera.fieldOfView = 94f;
   cameraObject.AddComponent<AudioListener>(); Weapon = cameraObject.AddComponent<IPViewWeapon>(); Weapon.Initialize(ViewCamera, this);
   Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
  }

  void Update()
  {
   if (!initialized || ViewCamera == null || IPGame.IsPaused) return;
   // Timelines use elapsed game time. Capping it slowed every animation on a busy frame.
   float dt = Time.deltaTime;
   DamageFlash = Mathf.MoveTowards(DamageFlash, 0f, dt * 1.7f); HitConfirm = Mathf.MoveTowards(HitConfirm, 0f, dt * 5f);
   trauma = Mathf.MoveTowards(trauma, 0f, dt * 1.9f); recoil = Mathf.MoveTowards(recoil, 0f, dt * 15f);
   landingKick = Mathf.Lerp(landingKick, 0f, 1f - Mathf.Exp(-dt * 12f)); damageLean = Mathf.Lerp(damageLean, 0f, 1f - Mathf.Exp(-dt * 9f));
   shotLateral *= Mathf.Exp(-dt * 17f); shotPush *= Mathf.Exp(-dt * 22f);
   hitProtection -= dt; dashCooldown = Mathf.Max(0f, dashCooldown - dt);
   var keyboard = Keyboard.current; var mouse = Mouse.current;
   bool capturedThisFrame = false;
   if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked && !IsDead)
   { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; capturedThisFrame = true; }
   if (IsExecuting) { UpdateExecution(dt); Weapon.Tick(dt, Vector2.zero, 0f, false, false); return; }
   if (IsDead)
   {
    CancelSpecialAttack();
    ViewCamera.transform.localRotation = Quaternion.Slerp(ViewCamera.transform.localRotation, Quaternion.Euler(16f, 0f, -18f), dt * 2f);
    ViewCamera.transform.localPosition = Vector3.Lerp(ViewCamera.transform.localPosition, Vector3.up * .52f, dt * 2f);
    Weapon.Tick(dt, Vector2.zero, 0f, true, false); return;
   }
   bool hasControl = Cursor.lockState == CursorLockMode.Locked;
   mouseDelta = hasControl && mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
   yaw += mouseDelta.x * .105f; pitch = Mathf.Clamp(pitch - mouseDelta.y * .105f, -86f, 86f); transform.rotation = Quaternion.Euler(0f, yaw, 0f);
   Vector2 input = Vector2.zero;
   if (keyboard != null && hasControl)
   {
    input.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
    input.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f); input = Vector2.ClampMagnitude(input, 1f);
    if (keyboard.spaceKey.wasPressedThisFrame) jumpBuffer = .12f;
    if (keyboard.digit1Key.wasPressedThisFrame) Weapon.SwitchWeapon(0);
    if (keyboard.digit2Key.wasPressedThisFrame) Weapon.SwitchWeapon(1);
    if (keyboard.digit3Key.wasPressedThisFrame) Weapon.SwitchWeapon(2);
    if (keyboard.fKey.wasPressedThisFrame) TrySpecialAttack();
   }
   if (hasControl && mouse != null && Mathf.Abs(mouse.scroll.ReadValue().y) > .1f) Weapon.CycleWeapon(mouse.scroll.ReadValue().y > 0f ? 1 : -1);
   jumpBuffer -= dt; wasGrounded = grounded; grounded = controller.isGrounded;
   if (!grounded && verticalVelocity <= 0f) grounded = Physics.SphereCast(transform.position + Vector3.up * .34f, .24f, Vector3.down, out _, .15f, ~(1 << 2), QueryTriggerInteraction.Ignore);
   if (grounded)
   {
    coyoteTime = .11f; JumpsRemaining = 2; if (verticalVelocity < 0f) verticalVelocity = -2.5f;
    if (!wasGrounded) { landingKick = -.09f; IPGame.Audio("land", transform.position, .35f); }
   }
   else coyoteTime -= dt;
   if (jumpBuffer > 0f && (JumpsRemaining > 0 || coyoteTime > 0f))
   {
    bool airJump = !grounded && coyoteTime <= 0f;
    if (airJump && JumpsRemaining == 2) JumpsRemaining = 1;
    verticalVelocity = airJump ? 10.1f : 10.4f; JumpsRemaining = Mathf.Max(0, JumpsRemaining - 1);
    jumpBuffer = 0f; coyoteTime = 0f; grounded = false; landingKick = airJump ? -.07f : -.025f;
    IPGame.Audio(airJump ? "doublejump" : "jump", transform.position, .6f);
    if (airJump) IPGame.Burst(transform.position + Vector3.up * .12f, new Color(.08f, .8f, 1f), 12, 3f);
   }
   Vector3 desiredDirection = transform.right * input.x + transform.forward * input.y;
   if (hasControl && keyboard != null && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame) && dashCooldown <= 0f)
   {
    dashDirection = desiredDirection.sqrMagnitude > .01f ? desiredDirection.normalized : transform.forward;
    dashRemaining = .165f; dashCooldown = DashRecovery; trauma = Mathf.Max(trauma, .18f);
    IPGame.Audio("dash", transform.position, .75f); IPGame.Burst(transform.position + Vector3.up * .4f, new Color(.04f, .6f, 1f), 8, 3f);
   }
   if (dashRemaining > 0f) { dashRemaining -= dt; horizontalVelocity = dashDirection * 31f; verticalVelocity = Mathf.Max(verticalVelocity, -.5f); }
   else
   {
    float acceleration = grounded ? (input.sqrMagnitude > .01f ? 72f : 58f) : 27f;
    horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredDirection * Speed, acceleration * dt); verticalVelocity -= 29f * dt;
   }
   // Controller steps stay small while the camera and weapon advance once per rendered frame.
   var collision = CollisionFlags.None;
   int steps = Mathf.Clamp(Mathf.CeilToInt(dt / .025f), 1, 12);
   for (int step = 0; step < steps; step++) collision |= controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * (dt / steps));
   if ((collision & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
   if (transform.position.y < -24f) ResetAt(spawnPoint);
   UpdateSpecialAttack(dt);ApplySpecialMotion(dt);
   float movement = Mathf.Clamp01(new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude / Speed);
   UpdateCamera(dt, movement, input.x); Weapon.Tick(dt, mouseDelta, movement, grounded, dashRemaining > 0f);
   if (hasControl && !IsSpecialAttacking && !capturedThisFrame && mouse != null && mouse.leftButton.isPressed) Weapon.TryFire();
   if (hasControl && !IsSpecialAttacking && keyboard != null && keyboard.rKey.wasPressedThisFrame) Weapon.Reload();
   candidateTimer -= dt;
   if (candidateTimer <= 0f) { candidateTimer = .055f; FindExecutionTarget(); }
   if (hasControl && keyboard != null && keyboard.eKey.wasPressedThisFrame && ExecutionCandidate != null) BeginExecution(ExecutionCandidate);
  }

  void UpdateCamera(float dt, float movement, float strafe)
  {
   if (grounded) bobPhase += dt * (movement > .1f ? 13.5f : 1.5f);
   float bob = grounded ? Mathf.Sin(bobPhase * 2f) * .025f * movement : 0f;
   float side = grounded ? Mathf.Cos(bobPhase) * .018f * movement : 0f;
   float rollTarget = -strafe * 2.6f - mouseDelta.x * .035f;
   if (dashRemaining > 0f) rollTarget -= Vector3.Dot(dashDirection, transform.right) * 7f;
   cameraRoll = Mathf.Lerp(cameraRoll, rollTarget, 1f - Mathf.Exp(-dt * 10f));
   Vector3 noise = new Vector3(Mathf.PerlinNoise(Time.time * 19f, 1.3f) - .5f, Mathf.PerlinNoise(9.4f, Time.time * 23f) - .5f, 0f) * Mathf.Min(trauma, .8f) * .052f;
   ViewCamera.transform.localPosition = new Vector3(side, EyeHeight + bob + landingKick, -shotPush) + noise;
   ViewCamera.transform.localRotation = Quaternion.Euler(pitch - recoil, shotLateral * .36f, cameraRoll + damageLean + shotLateral);
   float targetFov = 94f + movement * 4f + (dashRemaining > 0f ? 13f : 0f) + recoil * .55f;
   targetFov+=SpecialFovOffset();
   ViewCamera.fieldOfView = Mathf.Lerp(ViewCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-dt * 9f));
   ApplySpecialCamera();
   UpdateFootsteps(dt,movement);
  }

  // Positions refer to feet. Selection is captured before the execution clears player momentum.
  public static IPExecutionStyle ClassifyExecution(Vector3 playerPosition, Vector3 velocity, Vector3 right, Vector3 forward,
                                                   Vector3 targetPosition, Vector3 targetForward, bool dashing)
  {
   float elevation = playerPosition.y - targetPosition.y;
   if (velocity.y < -1f && elevation > 1f) return IPExecutionStyle.AerialPlunge;
   if (velocity.y > 2f && elevation > .35f) return IPExecutionStyle.RisingCut;
   Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
   if (dashing || horizontal.magnitude > 18f) return IPExecutionStyle.DashThrust;
   float lateral = Vector3.Dot(horizontal, right.normalized);
   if (lateral < -3f) return IPExecutionStyle.StrafeLeft;
   if (lateral > 3f) return IPExecutionStyle.StrafeRight;
   Vector3 fromTarget = Vector3.ProjectOnPlane(playerPosition - targetPosition, Vector3.up).normalized;
   if (Vector3.Dot(Vector3.ProjectOnPlane(targetForward, Vector3.up).normalized, fromTarget) < -.35f) return IPExecutionStyle.RearCut;
   return IPExecutionStyle.Frontal;
  }

  bool VisibleTarget(IPEnemy enemy)
  {
   Vector3 delta = enemy.ExecutionPoint - ViewCamera.transform.position;
   if (delta.sqrMagnitude < .001f) return true;
   if (Physics.Raycast(ViewCamera.transform.position, delta.normalized, out var hit, delta.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
    return hit.collider.GetComponentInParent<IPEnemy>() == enemy;
   return true;
  }

  void FindExecutionTarget()
  {
   ExecutionCandidate = null; float bestScore = float.MaxValue;
   foreach (var enemy in IPEnemy.Active)
   {
    if (enemy == null || !enemy.CanExecute) continue;
    Vector3 feetDelta = enemy.transform.position - transform.position;
    float horizontal = new Vector2(feetDelta.x, feetDelta.z).magnitude;
    bool diving = verticalVelocity < -1f && feetDelta.y < -1f && feetDelta.y > -5.2f && horizontal <= 3.25f;
    Vector3 delta = enemy.ExecutionPoint - ViewCamera.transform.position; float distance = delta.magnitude;
    float facing = distance > .01f ? Vector3.Dot(ViewCamera.transform.forward, delta / distance) : 1f;
    if (distance > (diving ? 6.1f : 4.1f) || facing < (diving ? -.24f : .56f) || !VisibleTarget(enemy)) continue;
    float score = horizontal + (diving ? Mathf.Abs(feetDelta.y) * .12f : distance * .35f) + (1f - facing) * (diving ? .3f : 2f);
    if (score < bestScore) { bestScore = score; ExecutionCandidate = enemy; }
   }
  }

  public void KickCamera(float amount) { AddShotImpulse(amount); }
  public void AddShotImpulse(float amount, float lateral = 0f)
  {
   recoil = Mathf.Min(9f, recoil + Mathf.Max(0f, amount));
   shotLateral = Mathf.Clamp(shotLateral + lateral, -2.8f, 2.8f);
   shotPush = Mathf.Min(.052f, shotPush + amount * .007f);
   trauma = Mathf.Max(trauma, Mathf.Clamp(amount * .055f, .1f, .3f));
  }
  public void ConfirmHit() { HitConfirm = 1f; IPGame.Hit();AddUltimateCharge(3f); }
  public void TakeDamage(float damage, Vector3 source)
  {
   if (IsDead || IsExecuting || IsUltimateActive || hitProtection > 0f || damage <= 0f) return;
   hitProtection = .18f; Health = Mathf.Max(0f, Health - damage); DamageFlash = 1f; trauma = .7f;
   damageLean = Vector3.Dot(transform.right, source - transform.position) > 0f ? -5f : 5f;
   IPGame.Audio("damage", transform.position, .6f);
   if (IsDead) { horizontalVelocity = Vector3.zero; IPGame.Label("VITAL SIGNS LOST  //  R TO RESTART", new Color(1f, .24f, .12f)); }
  }
  public void Heal(float amount) { if (!IsDead) Health = Mathf.Clamp(Health + amount, 0f, 100f); }
  public void AddShells(int amount) { if (Weapon != null) Weapon.AddShells(amount); }

  bool SafeExecutionRoute(Vector3 destination, IPEnemy target)
  {
   Vector3 delta = destination - transform.position;
   if (delta.sqrMagnitude < .002f) return true;
   float radius = controller.radius * .88f;
   Vector3 bottom = transform.position + Vector3.up * (radius + .09f);
   Vector3 top = transform.position + Vector3.up * (controller.height - radius);
   foreach (var hit in Physics.CapsuleCastAll(bottom, top, radius, delta.normalized, delta.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
   {
    if (hit.collider.GetComponentInParent<IPEnemy>() == target || hit.collider.transform.IsChildOf(transform)) continue;
    if (hit.normal.y > .55f) continue;
    return false;
   }
   return true;
  }

  public void BeginExecution(IPEnemy target)
  {
   if (target == null || !target.CanExecute || IsExecuting || IsSpecialAttacking || IsDead || controller == null || !VisibleTarget(target)) return;
   Vector3 separation = target.transform.position - transform.position;
   bool elevated = verticalVelocity < -1f && -separation.y > 1f;
   if (Vector3.ProjectOnPlane(separation, Vector3.up).magnitude > (elevated ? 3.3f : 4.2f) || Mathf.Abs(separation.y) > 5.2f) return;
   IPExecutionStyle style = ClassifyExecution(transform.position, Velocity, transform.right, transform.forward, target.transform.position, target.transform.forward, dashRemaining > 0f);
   Vector3 away = Vector3.ProjectOnPlane(transform.position - target.transform.position, Vector3.up);
   if (away.sqrMagnitude < .02f) away = -transform.forward;
   away.Normalize();
   Vector3 tangent = Vector3.Cross(Vector3.up, away);
   Vector3 staging = away;
   if (style == IPExecutionStyle.StrafeLeft) staging = (away - tangent * .28f).normalized;
   if (style == IPExecutionStyle.StrafeRight) staging = (away + tangent * .28f).normalized;
   Vector3 destination = target.transform.position + staging * (style == IPExecutionStyle.DashThrust ? 1.43f : 1.35f) + Vector3.up * .07f;
   if (!SafeExecutionRoute(destination, target)) return;
   CurrentExecutionStyle = LastExecutionStyle = style;
   executionTarget = target; executionStart = transform.position; executionEnd = destination;
   executionFocus = target.ExecutionPoint; executionRight = transform.right; executionForward = transform.forward;
   executionCut = style == IPExecutionStyle.AerialPlunge ? Vector3.down : style == IPExecutionStyle.RisingCut ? (Vector3.up + executionRight * .35f).normalized :
                  style == IPExecutionStyle.StrafeLeft ? -executionRight : style == IPExecutionStyle.StrafeRight ? executionRight :
                  style == IPExecutionStyle.DashThrust ? executionForward : (executionRight + Vector3.down * .42f).normalized;
   executionDuration = style == IPExecutionStyle.AerialPlunge ? 1.24f : style == IPExecutionStyle.RearCut ? 1.14f : style == IPExecutionStyle.Frontal ? 1.12f : style == IPExecutionStyle.DashThrust ? .92f : 1.04f;
   executionTime = executionHitStop = 0f; executionHit = executionGrabHit = false; executionLookStart = ViewCamera.transform.rotation;
   executionEyeStart = ViewCamera.transform.localPosition; executionFovStart = ViewCamera.fieldOfView;
   IsExecuting = true; ExecutionCandidate = null; horizontalVelocity = Vector3.zero; verticalVelocity = 0f; dashRemaining = 0f;
   target.BeginExecution(style,executionEnd); IPGame.Audio("execute_start", transform.position, .85f);
   Weapon.SetExecutionVictim(target.ExecutionShoulderPoint,target.ExecutionChestPoint,target.ExecutionHeadPoint);Weapon.StartExecution(style);
  }

  void UpdateExecution(float dt)
  {
   // The brief contact hold belongs to this action only; enemies/projectiles keep their clock.
   float held = Mathf.Min(Mathf.Max(executionHitStop, 0f), dt);
   executionHitStop = Mathf.Max(0f, executionHitStop - dt);
   executionTime += dt - held * .82f;
   float t = Mathf.Clamp01(executionTime / executionDuration);
   float reachTime = CurrentExecutionStyle == IPExecutionStyle.AerialPlunge ? .47f : .29f;
   float approach = SmootherStep(t / reachTime);
   Vector3 desired = Vector3.Lerp(executionStart, executionEnd, approach);
   float pathArc=Mathf.Sin(approach*Mathf.PI);
   float pathSide=CurrentExecutionStyle==IPExecutionStyle.StrafeLeft?-1f:1f;
   if(CurrentExecutionStyle!=IPExecutionStyle.AerialPlunge)desired+=executionRight*(pathArc*.115f*pathSide)+Vector3.up*(pathArc*.035f);
   if (CurrentExecutionStyle == IPExecutionStyle.RisingCut)
   {
    float lift = Mathf.Sin(Mathf.Clamp01(t / .76f) * Mathf.PI);
    desired.y += lift * lift * .23f;
   }
   // CharacterController resolves each frame, including the downward plunge; no transform teleport through level geometry.
   if (controller.enabled)
   {
    Vector3 delta = desired - transform.position;
    int steps = Mathf.Clamp(Mathf.CeilToInt(delta.magnitude / .2f), 1, 20);
    for (int step = 0; step < steps; step++) controller.Move(delta / steps);
   }
   if (executionTarget != null)
   {
    executionTarget.StageExecution(t);
    executionFocus=Vector3.Lerp(executionFocus,Vector3.Lerp(executionTarget.ExecutionChestPoint,executionTarget.ExecutionHeadPoint,.27f),1f-Mathf.Exp(-dt*18f));
   }
   Vector3 eyeOffset = Vector3.Lerp(executionEyeStart, Vector3.up * EyeHeight, SmootherStep(t / .26f));
   Vector3 eye = transform.TransformPoint(eyeOffset);
   Vector3 lookDelta = executionFocus + Vector3.up * .03f - eye;
   if (lookDelta.sqrMagnitude < .01f) lookDelta = executionForward;
   Quaternion look = Quaternion.LookRotation(lookDelta.normalized);
   float impactAt = CurrentExecutionStyle == IPExecutionStyle.Frontal ? .58f : .52f;
   float punch = ContactPulse(t, impactAt, .075f);
   float handImpact=ContactPulse(t,.31f,.065f);
   float rip = (CurrentExecutionStyle == IPExecutionStyle.Frontal || CurrentExecutionStyle == IPExecutionStyle.RearCut || CurrentExecutionStyle == IPExecutionStyle.DashThrust) ? ContactPulse(t, .735f, .075f) : 0f;
   float wind = Mathf.Sin(Mathf.Clamp01(t / .87f) * Mathf.PI);
   wind *= wind;
   float side = CurrentExecutionStyle == IPExecutionStyle.StrafeLeft ? -1f : 1f;
   float roll = CurrentExecutionStyle == IPExecutionStyle.AerialPlunge ? -wind * 4.8f + punch * 4f : CurrentExecutionStyle == IPExecutionStyle.RisingCut ? wind * 5f : -wind * 4f * side;
   // One directed impact arc reads as weight without the old high frequency camera oscillation.
   Vector3 shake = executionRight * (side * (-punch * .016f + rip * .012f + handImpact*.013f)) + Vector3.up * (-punch * .039f - rip * .02f - handImpact*.025f);
   ViewCamera.transform.position = eye + shake;
   ViewCamera.transform.rotation = Quaternion.Slerp(executionLookStart, look, SmootherStep(t / .34f)) * Quaternion.Euler(-punch * 3.2f + rip * 1.6f-handImpact*1.7f, punch * 1.2f * side, roll);
   float fovWindow = Mathf.Sin(t * Mathf.PI); fovWindow *= fovWindow;
   ViewCamera.fieldOfView = Mathf.Lerp(Mathf.Lerp(executionFovStart, 94f, SmootherStep(t)), CurrentExecutionStyle == IPExecutionStyle.AerialPlunge ? 88f : 86f, fovWindow) + punch * 3f;
   if(executionTarget!=null)Weapon.SetExecutionVictim(executionTarget.ExecutionShoulderPoint,executionTarget.ExecutionChestPoint,executionTarget.ExecutionHeadPoint);
   Weapon.SetExecutionProgress(t);
   if(!executionGrabHit&&t>=.31f){executionGrabHit=true;executionHitStop=.018f;}
   if (!executionHit && t >= impactAt)
   {
    executionHit = true; executionHitStop = .045f;
    if (executionTarget != null && !executionTarget.IsDead)
    {
     executionTarget.FinishExecution(CurrentExecutionStyle, executionCut); Heal(35f); ExecutionCount++;
     IPGame.Label(ExecutionLabel(CurrentExecutionStyle) + "  //  +35 HEALTH", new Color(.32f, 1f, .64f));
    }
    HitConfirm = 1f; IPGame.Hit(true);
   }
   if (t >= 1f)
   {
    Quaternion finalRotation = ViewCamera.transform.rotation;
    Vector3 finalPosition = ViewCamera.transform.position;
    Vector3 angles = finalRotation.eulerAngles;
    yaw = angles.y; pitch = Mathf.Clamp(angles.x > 180f ? angles.x - 360f : angles.x, -80f, 80f);
    transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    // Changing the body yaw used to rotate the eye a second time for a single frame.
    ViewCamera.transform.SetPositionAndRotation(finalPosition, Quaternion.Euler(pitch, yaw, 0f));
    cameraRoll = damageLean = shotLateral = shotPush = recoil = trauma = 0f;
    landingKick = ViewCamera.transform.localPosition.y - EyeHeight;
    IsExecuting = false; executionTarget = null; hitProtection = .4f;
    verticalVelocity = -2.5f; Weapon.EndExecution();
   }
  }

  static float SmootherStep(float value) { float t = Mathf.Clamp01(value); return t * t * t * (t * (t * 6f - 15f) + 10f); }
  static float ContactPulse(float time, float contact, float duration)
  {
   float t = (time - contact) / duration;
   if (t <= 0f || t >= 1f) return 0f;
   float wave = Mathf.Sin(t * Mathf.PI); return wave * wave;
  }

  static string ExecutionLabel(IPExecutionStyle style)
  {
   switch (style)
   {
    case IPExecutionStyle.AerialPlunge: return "KATANA // SKY PIERCER";
    case IPExecutionStyle.RisingCut: return "KATANA // RISING SEVER";
    case IPExecutionStyle.DashThrust: return "KATANA // IMPALEMENT";
    case IPExecutionStyle.StrafeLeft: return "KATANA // LEFT CROSSCUT";
    case IPExecutionStyle.StrafeRight: return "KATANA // RIGHT CROSSCUT";
    case IPExecutionStyle.RearCut: return "GRAB // TURN AND TEAR";
    default: return "KATANA // DOUBLE DISSECTION";
   }
  }

  public void ResetAt(Vector3 spawn)
  {
   ResetSpecialCombat();
   if (executionTarget != null && !executionTarget.IsDead) executionTarget.CancelExecution();
   spawnPoint = spawn; if (controller != null) controller.enabled = false;
   transform.position = spawn; Health = 100f; horizontalVelocity = Vector3.zero; verticalVelocity = 0f;
   pitch = 0f; yaw = transform.eulerAngles.y; dashRemaining = dashCooldown = 0f; trauma = recoil = DamageFlash = 0f;
   IsExecuting = false; executionTime = executionHitStop = 0f; CurrentExecutionStyle = IPExecutionStyle.Frontal;
   cameraRoll = landingKick = damageLean = shotLateral = shotPush = hitProtection = coyoteTime = jumpBuffer = candidateTimer = HitConfirm = 0f; grounded = wasGrounded = false;
   if (ViewCamera != null) { ViewCamera.transform.localPosition = Vector3.up * EyeHeight; ViewCamera.transform.localRotation = Quaternion.identity; ViewCamera.fieldOfView = 94f; }
   executionTarget = ExecutionCandidate = null; JumpsRemaining = 2; ExecutionCount = 0;
   if (controller != null) controller.enabled = true;
   if (Weapon != null) { Weapon.EndExecution(); Weapon.ResetAmmo(); }
   Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
  }
 }
}


