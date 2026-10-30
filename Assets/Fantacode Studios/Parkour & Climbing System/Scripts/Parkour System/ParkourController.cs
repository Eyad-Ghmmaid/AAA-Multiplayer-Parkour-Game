using FS_Core;
using FS_LocomotionSystem;
using FS_Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
namespace FS_Core
{
    public static partial class AnimatorParameters
    {
        public static int freeHang = Animator.StringToHash("freeHang");
        public static int jumpBackDirection = Animator.StringToHash("jumpBackDirection");
        public static int BackJumpDir = Animator.StringToHash("BackJumpDir");
        public static int BackJumpMode = Animator.StringToHash("BackJumpMode");
        public static int mirrorJump = Animator.StringToHash("mirrorJump");
        public static int isFalling = Animator.StringToHash("isFalling");
    }
}

namespace FS_ParkourSystem
{
    [RequireComponent(typeof(ParkourCore))]
    public class ParkourController : SystemBase, IParkourBridge
    {
        //[field: Header("Predictive Jumping")]
        [field: Tooltip("Enables Predictive Jumping")]
        public bool enablePredictiveJump = true;

        [field: Space(10)]

        [field: Tooltip("Maximum height that the player can jump")]
        [field: SerializeField] public float MaxJumpHeight { get; private set; } = 1.2f;

        [field: Tooltip("The character should only jump when he's close to the ledge. This is the distance from ledge at which he will jump")]
        [field: SerializeField] float minDistanceFromLedgeForJumping = .5f;

        [field: SerializeField] public float MaxJumpDistance { get; private set; } = 7f;

        [Tooltip("Radius of the foot collider. Foot collider is used check if the player's foot landed on any obstacle during the predictive jump")]
        [SerializeField] float footColliderRadius = 0.1f;

        [Tooltip("Offset between the foot collider and the foot bone")]
        [SerializeField] Vector3 footColliderOffset;

        [Tooltip("List of Parkour Actions the player can perform while standing close to the obstacle")]
        public List<ParkourAction> parkourActions = new List<ParkourAction>();

        public bool EnableWallRun;

        [ShowIf("EnableWallRun", true)]
        public ParkourAction wallRunAction;

        [HideInInspector] public List<PredictiveAction> predictiveActions = new List<PredictiveAction>();

        public ActionExecutor ActionExecutor { get; private set; }
        public bool InAction { get => parkourCore.InAction; set => parkourCore.InAction = value; }
        public bool IsHanging { get => parkourCore.IsHanging; set => parkourCore.IsHanging = value; }
        public bool IsOnLedge => isOnLedge;

        public bool ControlledByParkour => InAction || IsHanging;


        Vector3 prevPos = Vector3.zero;

        // Predictive jump state redirects (owned by ParkourCore)
        bool inPredictiveJump { get => parkourCore.InPredictiveJump; set => parkourCore.InPredictiveJump = value; }
        bool inAirOfPredictiveJump { get => parkourCore.InAirOfPredictiveJump; set => parkourCore.InAirOfPredictiveJump = value; }
        bool isOnLedge = false;

        ParkourInputManager inputManager;
        LocomotionInputManager locomotionInputManager;
        EnvironmentScanner environmentScanner;
        Animator animator;
        PlayerController playerController;
        ICharacter player;
        Damagable damagable;
        ClimbController climbController;
        ClimbCore climbEngine;
        LocomotionCore locomotionCore;
        ParkourCore parkourCore;

        public override SystemState State { get; } = SystemState.Parkour;
        public override float Priority => 10;
        public override bool IsUnreachableByGround => true;

        public override bool PermitsWhileFocused(SystemState state) =>
           inAirOfPredictiveJump && (state == SystemState.GrapplingHook || state == SystemState.Swing);

        private void OnEnable()
        {
            playerController = GetComponent<PlayerController>();
            locomotionCore = GetComponent<LocomotionCore>();
            player = GetComponent<ICharacter>();
            damagable = GetComponent<Damagable>();
            animator = player.Animator;
            environmentScanner = GetComponent<EnvironmentScanner>();
            inputManager = GetComponent<ParkourInputManager>();
            locomotionInputManager = GetComponent<LocomotionInputManager>();


            climbController = GetComponent<ClimbController>();
            climbEngine = GetComponent<ClimbCore>();
            ActionExecutor = GetComponent<ActionExecutor>();
            parkourCore = GetComponent<ParkourCore>();
            parkourCore.Bridge = this;
            animator.SetFloat(AnimatorParameters.fallAmount, 1);
        }


        public override void HandleFixedUpdate()
        {
            HandleParkourUpdate();
        }

        void HandleParkourUpdate()
        {
            // If the character is in a predictiveJump or hanging then return
            if (inAirOfPredictiveJump || IsHanging)
            {
                prevPos = transform.position;
                isOnLedge = false;
                return;
            }

            // Pefrom Parkour Actions or Predictive Jump/Climb
            if (locomotionInputManager.JumpHold && player.IsGrounded)
            {
                var hitData = environmentScanner.ObstacleCheck();
                HandleParkourAction(hitData);

                HandlePredictiveJumpAndClimb();

                if (hitData.forwardHitFound && !InAction && Vector3.Angle(Vector3.up, hitData.forwardHit.normal) > 60f && !climbController.isFalling && EnableWallRun)
                    StartCoroutine(HandleWallRun(hitData));
            }

            prevPos = transform.position;
        }

        void HandleParkourAction(ObstacleHitData hitData)
        {
            if (!InAction && hitData.forwardHitFound && hitData.heightHitFound && hitData.hasSpace
                && Vector3.Angle(Vector3.up, hitData.forwardHit.normal) > 45f
                && Vector3.Angle(Vector3.up, hitData.heightHit.normal) <= 45f)
            {
                List<ParkourAction> selectedActions = new();
                foreach (var action in parkourActions)
                {
                    if (action.CheckIfPossible(hitData, transform))
                    {
                        selectedActions.Add(action);
                    }
                }

                List<ParkourAction> priorityActions = new();

                foreach (var action in selectedActions)
                {
                    if (animator.GetFloat("moveAmount") >= action.movementThreshold)
                    {
                        StartCoroutine(parkourCore.DoParkourAction(action));
                        return;
                    }

                }
                if (selectedActions.Count > 0)
                {
                    StartCoroutine(parkourCore.DoParkourAction(selectedActions[UnityEngine.Random.Range(0, selectedActions.Count)]));
                    return;
                }
            }
        }

        void HandlePredictiveJumpAndClimb(Vector3? direction = null)
        {
            Vector3 _direction = direction != null ? direction.Value : player.MoveDir;

            if ((enablePredictiveJump || climbController.enableClimbing) && !InAction || (InAction && inPredictiveJump))
            {
                var jumpData = environmentScanner.FindPointToJump(_direction, MaxJumpDistance, MaxJumpHeight);
                if (jumpData != null)
                {
                    // Predictive climb should be performed when a jumpPoint is not found or if there is a climbPoint that's closer than the jumpPoint
                    if (climbController.enableClimbing && jumpData.isClimbable
                        && (!jumpData.jumpPointFound || (jumpData.jumpPointFound && Vector3.Distance(jumpData.climbHitData.point, transform.position) < Vector3.Distance(jumpData.rootPosition, transform.position))))
                    {
                        // if there is a ledge The character should only jump when he's close to the ledge
                        if (!jumpData.hasLedge || Vector3.Distance(transform.position, jumpData.pointBeforeledge) < minDistanceFromLedgeForJumping)
                            if (climbController.ClimbToLedge(jumpData.climbHitData.transform, jumpData.climbHitData.point, checkDirection: direction))
                                return;
                    }
                    if (enablePredictiveJump && jumpData.jumpPointFound)
                    {
                        // if there is a ledge The character should only jump when he's close to the ledge
                        if (jumpData.hasLedge && Vector3.Distance(transform.position, jumpData.pointBeforeledge) > minDistanceFromLedgeForJumping)
                            return;

                        if (customPredictiveAction)
                        {
                            List<PredictiveAction> selectedActions = new();
                            foreach (var action in predictiveActions)
                            {
                                if (action.CheckIfPossible(jumpData, transform))
                                {
                                    selectedActions.Add(action);
                                }
                            }
                        }

                        StartCoroutine(parkourCore.DoPredictiveJump(jumpData));
                        return;
                    }
                }
            }
        }

        bool customPredictiveAction;
        Transform jumpTarget;

        public void ResetPositionY()
        {
            if (Physics.SphereCast(transform.position + Vector3.up * 0.6f, 0.2f, Vector3.down, out RaycastHit hit, 1f, environmentScanner.ObstacleLayer))
                StartCoroutine(TweenVal(transform.position.y, hit.point.y, 0.2f, (lerpVal) => { transform.position = new Vector3(transform.position.x, lerpVal, transform.position.z); })); ;
        }

        public IEnumerator TweenVal(float start, float end, float duration, Action<float> onLerp)
        {
            float timer = 0f;
            float percent = timer / duration;

            while (percent <= 1f)
            {
                timer += Time.deltaTime;
                percent = timer / duration;
                var lerpVal = Mathf.Lerp(start, end, percent);
                onLerp?.Invoke(lerpVal);

                yield return null;
            }
        }

        public void HandlePredictiveBackJump(Vector3 checkDir)
        {
            var jumpData = GetComponent<EnvironmentScanner>().FindPointToJump(checkDir, 4.5f, 1f, false);
            if (jumpData != null)
            {
                if (jumpData.isClimbable && (!jumpData.jumpPointFound || (jumpData.jumpPointFound && jumpData.climbHitData.point.y > jumpData.rootPosition.y)))
                {
                    if (jumpData.climbHitData.point.y - transform.position.y < 2f)
                    {
                        animator.SetFloat(AnimatorParameters.BackJumpDir, 0);
                        animator.SetBool(AnimatorParameters.BackJumpMode, false);
                        if (climbController.ClimbToLedge(jumpData.climbHitData.transform, jumpData.climbHitData.point, checkDirection: checkDir))//transform.position);
                            return;
                    }
                }
                if (jumpData.jumpPointFound)
                {
                    animator.SetFloat(AnimatorParameters.BackJumpDir, 0);
                    animator.SetBool(AnimatorParameters.BackJumpMode, false);
                    StartCoroutine(parkourCore.DoPredictiveBackJump(jumpData, climbCore: climbEngine, climbBridge: climbController));
                    return;
                }
            }
        }

        IEnumerator HandleWallRun(ObstacleHitData hitData)
        {
            if (wallRunAction == null)
                yield break;

            if (!Physics.SphereCast(transform.position + Vector3.up * 0.3f, 0.1f, Vector3.down, out RaycastHit hit, 0.5f, environmentScanner.ObstacleLayer))
                yield break;

            InAction = true;
            inPredictiveJump = true;

            OnStartSystem(this);
            if (player.WaitToStartSystem) yield return new WaitUntil(() => player.WaitToStartSystem == false);

            var action = wallRunAction;
            var warpParams = new MotionWarpParams()
            {
                matchPos = hitData.forwardHit.point,
                boneRootOffset = action.BoneRootOffset,
                startTime = action.MatchStartTime,
                endTime = action.MatchTargetTime,
                scaleRootMotion = action.WarpMatchType == MotionWarpType.ScaleRootMotion,
                weightMask = action.MatchPosWeight,
                rootCurves = action.RootMotionCurves,
            };

            var characterAction = new CharacterAction(wallRunAction.clipInfo)
            {
                usesRootMotion = true,
                ignoreCollision = true,
                requiresLedgeBlocking = false,
                motionWarp = warpParams != null,
                motionWarpParams = warpParams,
                animationParameters = new AnimParameters
                {
                    onAnimationUpdate = (normalizedTime, timer) =>
                    {
                        if (locomotionInputManager.JumpKeyDown)
                            HandlePredictiveJumpAndClimb(-transform.forward);

                        if (inAirOfPredictiveJump)
                        {
                            ActionExecutor.Cancel(wallRunAction.clipInfo);
                            return;
                        }

                        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(-hitData.forwardHit.normal),
                            200f * Time.deltaTime);
                    }
                }
            };

            yield return ActionExecutor.ExecuteAsync(characterAction);

            if (inAirOfPredictiveJump) yield break;

            
            climbController.isFalling = true;
            InAction = false;
            inPredictiveJump = false;

            OnEndSystem(this);
        }


        private void OnDrawGizmosSelected()
        {
            //if (inAirOfPredictiveJump)
            //{
            //    Gizmos.color = new Color(0, 1, 0, 0.5f);
            //}
        }

        private void OnDrawGizmos()
        {
            //environmentScanner?.actions?.Reverse();
            //environmentScanner?.actions?.ForEach(x => x.Invoke());

            //GizmosExtend.Show();
            //GizmosExtend.Clear();
            //if (_jumpPoint != null)
            //{
            //    Gizmos.color = new Color(0, 1, 0, 0.5f);
            //    Gizmos.DrawSphere(_jumpPoint.rootPosition, 0.1f);

            //    Gizmos.color = new Color(0, 0, 1, 0.5f);
            //    Gizmos.DrawSphere(_jumpPoint.footPosition, 0.1f);
            //}
            //Gizmos.color = Color.black;

            //if (matchPosGlobal != Vector3.zero)
            //    Gizmos.DrawSphere(matchPosGlobal, 0.2f);
        }
        public override void ExitSystem()
        {
            //animator.CrossFade(AnimationNames.FallTree, 0.2f);
            //IsHanging = false;
        }

        void OnStartSystem(SystemBase system, bool needHandsForAction = false)
        {
            player.FocusSystem(system, needHandsForAction);
        }

        void OnEndSystem(SystemBase system)
        {
            player.UnFocusSystem(system);
        }

        #region IParkourBridge

        float IParkourBridge.Gravity => player.Gravity;

        void IParkourBridge.OnActionStart(bool needHands) => OnStartSystem(this, needHands);
        void IParkourBridge.OnActionEnd() => OnEndSystem(this);
        bool IParkourBridge.WaitToStart => player.WaitToStartSystem;
        bool IParkourBridge.ShouldContinueAction => IsInFocus;

        void IParkourBridge.SetIsInAir(bool value) => playerController.IsInAir = value;
        void IParkourBridge.OnLand(float intensity, float duration) => playerController.OnLand?.Invoke(intensity, duration);

        float IParkourBridge.GetJumpHeight(float displacementY, Vector3 displacementXZ) =>
            environmentScanner.getJumpHeight(displacementY, displacementXZ);

        bool IParkourBridge.CheckGrounded(float timer, float jumpTime, Vector3 footPosition, Vector3 deltaPosition, bool mirrorJump)
        {
            if (timer <= 0.3f) return false;

            var jumpingFoot = mirrorJump ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
            var footTransform = animator.GetBoneTransform(jumpingFoot);
            var footPos = footTransform.position;

            if (!animator.GetBool(AnimatorParameters.isFalling))
                footPos = footTransform.TransformPoint(footColliderOffset);

            if (Physics.CheckSphere(footPos, footColliderRadius, environmentScanner.ObstacleLayer))
                return true;

            if (Physics.Linecast(transform.position - deltaPosition, transform.position, out RaycastHit hit, environmentScanner.ObstacleLayer))
            {
                transform.position = hit.point;
                return true;
            }

            var fp = animator.GetBoneTransform(jumpingFoot).position;
            if (Vector3.Distance(footPosition, fp) < 0.1f || timer > jumpTime)
                return true;

            return false;
        }

        #endregion
    }
}
