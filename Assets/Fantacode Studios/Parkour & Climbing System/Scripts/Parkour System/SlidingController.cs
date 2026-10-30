using FS_Core;
using FS_LocomotionSystem;
using FS_Util;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FC_ParkourSystem
{
    public class SlidingController : SystemBase
    {
        [Header("Slide Settings")]
        [SerializeField] private float slideSpeed = 12f;
        [SerializeField] private float slideAcceleration = 20f;
        [SerializeField] private float slideDeceleration = 8f;
        [SerializeField] private float minSlideSpeed = 4f;
        [SerializeField] private float slopeSlideBoost = 5f;
        [SerializeField] private float lateralControlStrength = 5f;
        [SerializeField] private float raycastDistance = 1.5f;

        [Header("Jump Settings")]
        [SerializeField] private float slideJumpHeight = 2f;
        [SerializeField] private float slideJumpForwardBoost = 1f;
        [SerializeField, Range(0f, .9f)] private float jumpForceTime = 0.65f;

        [Header("Animation")]
        [SerializeField] private AnimationClip slidingClip;
        [SerializeField] private AnimGraphClipInfo slideJumpClipInfo;

        private LocomotionInputManager inputManager;
        private PlayerController playerController;
        private IMotionDriver motionDriver;
        private ICharacter player;
        private BoneMapper boneMapper;
        private AnimGraph animGraph;
        private ActionExecutor actionExecutor;
        private LocomotionCore locomotionCore;

        private Vector3 currentSlideVelocity;
        private Vector3 slideDirection;
        private Vector3 currentSurfaceNormal = Vector3.up;
        private bool isInSlideJump;
        private bool justEntered;
        private float timeOffSlideSurface;
        private const float slideExitGracePeriod = 0.15f;
        private float lastJumpRequestTime = -10f;
        private const float jumpBufferWindow = 0.15f;
        private Transform hips;
        private Transform modelTransform;
        private bool slidingAnimActive;

        float sampleRadius = 2f;
        int sampleCount = 16;
        float sampleHeightOffset = 0.5f;
        float slopeAngleThreshold = 10f;
        float maxRaycastDistance = 5f;
        float gravity => Mathf.Abs(locomotionCore != null ? locomotionCore.gravity : -20f);

        public override SystemState State => SystemState.Sliding;

        public override float Priority => 20;

        private void Start()
        {
            inputManager = GetComponent<LocomotionInputManager>();
            playerController = GetComponent<PlayerController>();
            motionDriver = GetComponent<IMotionDriver>();
            player = GetComponent<ICharacter>();
            boneMapper = GetComponent<BoneMapper>();
            animGraph = GetComponent<AnimGraph>();
            actionExecutor = GetComponent<ActionExecutor>();
            locomotionCore = GetComponent<LocomotionCore>();
            hips = boneMapper.GetBone(BoneType.Hips);
            modelTransform = hips.GetComponentsInParent<Animator>().ToList().First().transform;
        }

        private void Update()
        {
            if (isInSlideJump) return;

            // Capture jump press into a small buffer so a quick tap isn't lost to
            // frame-ordering or a one-frame raycast hiccup.
            if (inputManager.JumpKeyDown)
                lastJumpRequestTime = Time.time;

            Vector3 rayOrigin = hips.position + Vector3.up * .5f + hips.forward * .2f;
            RaycastHit slideHit;
            bool isSlideSurface = Physics.SphereCast(rayOrigin, 0.3f, Vector3.down, out slideHit, raycastDistance, FSSettings.i.GroundLayer);

            if (!isSlideSurface)
                isSlideSurface = Physics.Raycast(rayOrigin, Vector3.down, out slideHit, raycastDistance, FSSettings.i.GroundLayer);

            bool onSlideSurface = isSlideSurface && slideHit.transform.CompareTag("Slide");

            if (onSlideSurface)
            {
                timeOffSlideSurface = 0f;

                if (!IsInFocus)
                {
                    // Capture seed velocity BEFORE FocusSystem resets it
                    Vector3 seed = locomotionCore.CurrentVelocity;
                    seed.y = 0;
                    
                    actionExecutor.CancelCurrent();
                    // InterruptPreviousState();
                    player.FocusSystem(this, true);
                    if (!IsInFocus) return; // Focus refused — bail
                    // ResetLocomotionState();

                    StartSlidingAnimation();

                    if (seed.sqrMagnitude < 0.01f) seed = transform.forward * minSlideSpeed;
                    currentSlideVelocity = seed.normalized * Mathf.Clamp(seed.magnitude, minSlideSpeed, slideSpeed);
                    justEntered = true;
                    motionDriver.Move(currentSlideVelocity * Time.deltaTime + Vector3.down * gravity * Time.deltaTime);

                    // Honor a jump press that happened on the entry frame
                    if (ConsumeJumpRequest())
                        InitiateSlideJump();
                    return;
                }

                justEntered = false;

                if (ConsumeJumpRequest())
                {
                    InitiateSlideJump();
                    return;
                }

                PerformSlide();
            }
            else
            {
                timeOffSlideSurface += Time.deltaTime;

                // The entry frame sets justEntered to protect a one-frame raycast miss right after
                // starting. Clear it on the next off-surface frame so the grace timer can actually
                // elapse and end the slide — otherwise a slide that started at the very edge of a
                // Slide surface (never getting an on-surface frame to clear it) would continue
                // forever on non-slide ground.
                bool wasJustEntered = justEntered;
                justEntered = false;

                if (IsInFocus && !wasJustEntered && timeOffSlideSurface >= slideExitGracePeriod)
                {
                    if (ConsumeJumpRequest())
                        InitiateSlideJump();
                    else
                    {
                        EndSlide();
                    }
                }
                else if (IsInFocus)
                {
                    // Still in slide focus during grace period — keep checking jump
                    // input so a press during a transient raycast miss isn't dropped.
                    if (ConsumeJumpRequest())
                    {
                        InitiateSlideJump();
                        return;
                    }
                    PerformSlide();
                }
            }
        }

        private bool ConsumeJumpRequest()
        {
            if (Time.time - lastJumpRequestTime <= jumpBufferWindow)
            {
                lastJumpRequestTime = -10f;
                return true;
            }
            return false;
        }

        private void EndSlide(bool preserveMomentum = false)
        {
            Vector3 v = currentSlideVelocity;
            StopSlidingAnimationImmediate();
            StartCoroutine(SmoothResetModelRotation());

            player.UnFocusSystem(this);
            // UnFocusSystem zeroes velocity. Only re-apply momentum for slide-jump
            // handoff so locomotion picks up the jump's natural arc; for a normal
            // surface exit, leave velocity at zero so the player transitions to the
            // correct movement state based on their actual input (no run-on).
            if (preserveMomentum)
            {
                Vector3 horizontal = new Vector3(v.x, 0, v.z);
                locomotionCore.SetCurrentVelocity(v.y, horizontal, ResetMoveParametersOnUnFocus, true);
            }

            currentSlideVelocity = Vector3.zero;
        }

        private void StopSlidingAnimationImmediate(float stopDelay = 0f)
        {
            if (!slidingAnimActive) return;
            slidingAnimActive = false;

            if (slidingClip == null) return;

            // Always tear the slide loop down with removeAll:true. The transition-back path (false)
            // bails out of LoopingAnimationTransitionback whenever the slide loop is no longer the
            // current playable — which happens intermittently on a normal exit (transition-in hadn't
            // settled / locomotion took over the same frame) and *always* on a slide-jump (the jump
            // action becomes the current playable), leaving the slide loop alive underneath and
            // resurfacing after the jump. removeAll forcibly reconnects the loop layer to locomotion
            // with no such guard. The slide-jump still delays the stop so the jump animation has
            // started before the loop is removed.
            if (stopDelay > 0f)
                StartCoroutine(AsyncUtil.RunAfterDelay(stopDelay, () => animGraph.StopLoopingAnimations(true)));
            else
                animGraph.StopLoopingAnimations(true);
        }

        IEnumerator SmoothResetModelRotation(float speed = 10f)
        {
            while (Quaternion.Angle(modelTransform.localRotation, Quaternion.identity) > 0.1f)
            {
                modelTransform.localRotation = Quaternion.Slerp(
                    modelTransform.localRotation,
                    Quaternion.identity,
                    speed * Time.deltaTime
                );
                yield return null;
            }
            modelTransform.localRotation = Quaternion.identity;
        }

        private void StartSlidingAnimation()
        {
            if (slidingAnimActive) return;
            slidingAnimActive = true;

            if (slidingClip != null)
                animGraph.PlayLoopingAnimation(slidingClip, transitionIn: 0f);
        }

        private void PerformSlide()
        {
            var slopeSample = GetSlideDirection();
            slideDirection = slopeSample.downhill;
            float slopeAngle = slopeSample.slopeAngle;
            Vector3 normal = slopeSample.normal;

            // Smooth surface normal for rotation
            if (normal.sqrMagnitude > 0.01f)
                currentSurfaceNormal = Vector3.Slerp(currentSurfaceNormal, normal, 10f * Time.deltaTime);

            float slopeBoost = (slopeAngle / 90f) * slopeSlideBoost;
            currentSlideVelocity += slideDirection * slopeBoost * Time.deltaTime;

            // Lateral control
            float inputX = inputManager.DirectionInput.x;
            if (Mathf.Abs(inputX) > 0)
            {
                Vector3 lateralAxis = Vector3.Cross(normal, slideDirection);
                if (lateralAxis.sqrMagnitude < 1e-8f)
                {
                    lateralAxis = Vector3.ProjectOnPlane(playerController.CameraPlanarRotation * Vector3.right, normal);
                    if (lateralAxis.sqrMagnitude < 1e-8f)
                        lateralAxis = playerController.transform.right;
                }
                lateralAxis.Normalize();

                Vector3 lateralControl = lateralAxis * inputX * lateralControlStrength;

                Vector3 upAlongSlope = Vector3.ProjectOnPlane(Vector3.up, normal);
                if (upAlongSlope.sqrMagnitude > 1e-6f) upAlongSlope.Normalize();
                float uphill = Vector3.Dot(lateralControl, upAlongSlope);
                if (uphill > 0f)
                    lateralControl -= upAlongSlope * uphill;

                float lateralComponent = Vector3.Dot(currentSlideVelocity, lateralAxis);
                float desiredLateral = lateralComponent + lateralControl.magnitude * Mathf.Sign(inputX) * Time.deltaTime;
                desiredLateral = Mathf.Clamp(desiredLateral, -lateralControlStrength, lateralControlStrength);
                Vector3 lateralPart = lateralAxis * desiredLateral;
                Vector3 otherPart = currentSlideVelocity - lateralAxis * lateralComponent;
                currentSlideVelocity = otherPart + lateralPart;
            }

            // Accelerate or decelerate
            float currentSpeed = currentSlideVelocity.magnitude;
            if (currentSpeed < slideSpeed)
                currentSlideVelocity += slideDirection * slideAcceleration * Time.deltaTime;
            else if (currentSpeed > slideSpeed)
                currentSlideVelocity = Vector3.Lerp(currentSlideVelocity, currentSlideVelocity.normalized * slideSpeed, slideDeceleration * Time.deltaTime);

            if (currentSlideVelocity.magnitude < minSlideSpeed)
                currentSlideVelocity = currentSlideVelocity.normalized * minSlideSpeed;

            // Movement
            Vector3 gravityForce = Vector3.down * gravity * Time.deltaTime;
            Vector3 moveVector = currentSlideVelocity * Time.deltaTime + gravityForce;

            // Rotation
            if (slideDirection != Vector3.zero)
            {
                var dir = moveVector;
                dir.y = 0;
                Quaternion targetRotation = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 20 * Time.deltaTime);

                var pitchRayOrigin = transform.position + transform.forward + Vector3.up * 0.5f;
                if (Physics.Raycast(pitchRayOrigin, Vector3.down, out RaycastHit hit, 3f, FSSettings.i.GroundLayer))
                {
                    float pitch = Vector3.SignedAngle(hit.normal, Vector3.up, -modelTransform.right);
                    Quaternion targetRot = Quaternion.Euler(pitch, modelTransform.eulerAngles.y, 0f);
                    modelTransform.rotation = Quaternion.Lerp(modelTransform.rotation, targetRot, 5 * Time.deltaTime);
                }
            }

            motionDriver.Move(moveVector);
        }

        public SlopeSample GetSlideDirection()
        {
            Vector3 origin = transform.position;
            Vector3 baseRayOrigin = origin + Vector3.up * sampleHeightOffset;

            var candidates = new List<SlopeSample>();

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float ang = t * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * sampleRadius;
                Vector3 rayStart = baseRayOrigin + offset;

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, maxRaycastDistance, FSSettings.i.GroundLayer, QueryTriggerInteraction.Ignore))
                {
                    float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

                    if (slopeAngle >= slopeAngleThreshold)
                    {
                        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, hit.normal);
                        if (downhill.sqrMagnitude > 0.0001f) downhill.Normalize();

                        candidates.Add(new SlopeSample
                        {
                            position = hit.point,
                            normal = hit.normal,
                            slopeAngle = slopeAngle,
                            downhill = downhill
                        });
                    }
                }
            }

            if (candidates.Count == 0)
                return new SlopeSample();

            SlopeSample best = candidates[0];
            foreach (var s in candidates)
                if (s.position.y < best.position.y) best = s;
            return best;
        }

        private void InitiateSlideJump()
        {
            StartCoroutine(SlideJumpRoutine());
        }

        private IEnumerator SlideJumpRoutine()
        {
            Vector3 boostDir = currentSlideVelocity.sqrMagnitude > 0.01f ? currentSlideVelocity.normalized : transform.forward;
            Vector3 jumpHorizontalVelocity = currentSlideVelocity * slideJumpForwardBoost;
            float verticalVelocity = 0f;
            bool jumpApplied = false;

            StopSlidingAnimationImmediate(stopDelay: 0.2f);
            StartCoroutine(SmoothResetModelRotation());

            isInSlideJump = true;
            currentSlideVelocity = Vector3.zero;

            CharacterAction jumpAction = null;
            System.Action<float, float> jumpUpdate = (normalizedTime, timer) =>
            {
                if (normalizedTime < jumpForceTime) return;

                if (!jumpApplied)
                {
                    if (jumpAction != null) jumpAction.usesRootMovement = false;
                    verticalVelocity = Mathf.Sqrt(2f * gravity * slideJumpHeight);
                    jumpApplied = true;
                }

                verticalVelocity -= gravity * Time.deltaTime;

                Vector3 move = jumpHorizontalVelocity * Time.deltaTime;
                move.y = verticalVelocity * Time.deltaTime;
                motionDriver.Move(move);
            };
            jumpAction = new CharacterAction(slideJumpClipInfo)
            {
                requiresLedgeBlocking = false,
                usesRootMotion = true,
                usesRootMovement = true,
                animationParameters = new AnimParameters
                {
                    onAnimationUpdate = jumpUpdate
                }
            };
            yield return actionExecutor.ExecuteAsync(jumpAction);

            StartCoroutine(AsyncUtil.RunAfterDelay(.5f, () => isInSlideJump = false));
            currentSlideVelocity = jumpHorizontalVelocity;
            currentSlideVelocity.y = verticalVelocity;
            EndSlide(preserveMomentum: true);
        }

        public override void ExitSystem()
        {
            currentSlideVelocity = Vector3.zero;
            StopSlidingAnimationImmediate();
            StartCoroutine(SmoothResetModelRotation());
        }

        public struct SlopeSample
        {
            public Vector3 position;
            public Vector3 normal;
            public float slopeAngle;
            public Vector3 downhill;
        }
    }
}
