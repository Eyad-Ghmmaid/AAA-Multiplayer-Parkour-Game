using FS_Core;
using System.Text;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
namespace FS_Core
{
    public class AnimatorLayerData
    {
        public string layerName;
        public int layer;
        public float currentWeight;
    }

    public static partial class AnimatorParameters
    {
        public static int moveAmount = Animator.StringToHash("moveAmount");
        public static int strafeAmount = Animator.StringToHash("strafeAmount");
        public static int IsGrounded = Animator.StringToHash("IsGrounded");
        public static int fallAmount = Animator.StringToHash("fallAmount");
        public static int locomotionType = Animator.StringToHash("locomotionType");
        public static int idleType = Animator.StringToHash("idleType");
        public static int crouchType = Animator.StringToHash("crouchType");
        public static int rotation = Animator.StringToHash("rotation");
        public static int turnback_Mirror = Animator.StringToHash("turnback Mirror");
        public static int runToStopAmount = Animator.StringToHash("RunToStopAmount");
        public static int mirrorAction = Animator.StringToHash("mirrorAction");
        public static int coverMode = Animator.StringToHash("CoverMode");
    }

    public static partial class AnimationNames
    {
        public static string FallTree = "FallTree";
        public static string FallIdle = "FallIdle";
        public static string Locomotion = "Locomotion";
        public static string LandAndStepForward = "LandAndStepForward";

    }

    public static partial class AnimatorLayer
    {
        public static AnimatorLayerData shootingLayer = new AnimatorLayerData() { layerName = "Shooting", layer = 2, currentWeight = 0 };
        public static AnimatorLayerData armLayer = new AnimatorLayerData() { layerName = "Arm Layer", layer = 1, currentWeight = 0 };
        public static AnimatorLayerData baseLayer = new AnimatorLayerData() { layerName = "Base Layer", layer = 0, currentWeight = 0 };
    }

    public class TargetMatchParams
    {
        public Vector3 pos;
        public Quaternion rot;
        public AvatarTarget target;
        public AvatarTarget startTarget;
        public float startTime;
        public float endTime;

        public Vector3 startPos;
        public Vector3 endPos;

        public Vector3 posWeight;
    }

}
namespace FS_LocomotionSystem 
{ 

    [RequireComponent(typeof(LocomotionCore))]
    public class LocomotionController : SystemBase, ICharacter
    {
        // Movement Parameters
        [Tooltip("Disables Sprinting")]
        public bool enableSprint = true;

        [Tooltip("Sets running to default")]
        public bool setDefaultStateToRunning = true;

        [Tooltip("If true, then player will always face the camera's forward even in idle")]
        public bool faceCameraForwardWhenIdle;

        // Public Properties
        public bool IsCrouching => locomotionCore.IsCrouching;
        public override float Priority => 100;

        public override SystemState State => SystemState.Locomotion;

        // Private/Internal Fields
        float sprintModeTimer = 0;
        float moveAmount;
        Vector3 desiredMoveDir;
        Vector3 moveInput;

        // Cached References
        PlayerController playerController;
        GameObject cameraGameObject;
        CharacterController characterController;
        CharacterCapsule characterCapsule;
        Animator animator;
        AnimGraph animGraph;
        EnvironmentScanner environmentScanner;
        LocomotionInputManager inputManager;
        ItemEquipper itemEquipper;
        LocomotionCore locomotionCore;


        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            animator = GetComponent<Animator>();
            animGraph = GetComponent<AnimGraph>();
            environmentScanner = GetComponent<EnvironmentScanner>();
            characterController = GetComponent<CharacterController>();
            characterCapsule = GetComponent<CharacterCapsule>();
            inputManager = GetComponent<LocomotionInputManager>();
            itemEquipper = GetComponent<ItemEquipper>();
            locomotionCore = GetComponent<LocomotionCore>();
            // Animator IK can run before Start. Cache the camera in Awake so the
            // callback never reads an uninitialized camera reference.
            cameraGameObject = playerController != null ? playerController.cameraGameObject : null;

            Physics.queriesHitTriggers = false;
        }

        void Start()
        {
            cameraGameObject = playerController.cameraGameObject;

            locomotionCore.OnLocomotionActionStart += () =>
            {
                PreventAllSystems = true;
                // FocusSystem(this);
            };

            locomotionCore.OnLocomotionActionEnd += () =>
            {
                PreventAllSystems = false;

                if (!locomotionCore.IsLocomotionLockActive)
                    UnFocusSystem(this);
            };

            locomotionCore.OnLandingActionStarted += () =>
            {
                FocusSystem(this);
            };

            locomotionCore.OnLanded += (float intensity, float duration) =>
            {
                playerController.OnLand?.Invoke(intensity, duration);
            };

            locomotionCore.OnLocomotionLockEnabled += () =>
            {
                FocusSystem(this);
            };
            locomotionCore.OnLocomotionLockDisabled += () =>
            {
                UnFocusSystem(this);
            };

            //if (!(groundLayer == (groundLayer | (1 << LayerMask.NameToLayer("Ledge")))))
            //    groundLayer += 1 << LayerMask.NameToLayer("Ledge");
            //if (LayerExists("Hotspot") && !(groundLayer == (groundLayer | (1 << LayerMask.NameToLayer("Hotspot")))))
            //    groundLayer += 1 << LayerMask.NameToLayer("Hotspot");
        }
        bool LayerExists(string layerName)
        {
            return LayerMask.NameToLayer(layerName) != -1;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || cameraGameObject == null)
                return;

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || head == null)
                return;

            var offset = Vector3.Distance(hips.position, head.position);
            animator.SetLookAtPosition(cameraGameObject.transform.position + cameraGameObject.transform.forward * (5f) + new Vector3(0, offset, 0));
            //animator.SetLookAtWeight(headIK);
        }

        public override void HandleFixedUpdate()
        {
            locomotionCore.HandleCrouching();
            locomotionCore.HandleBalancing();
            locomotionCore.ApplyStanceToAnimatorAndCollider();
        }

        private void Update()
        {
            GetInput();

            // If the current state was changed, then interupt actions in locomotion cores
            locomotionCore.InterruptActions = playerController.CurrentSystemState != State;

#if UNITY_ANDROID || UNITY_IOS
            if (setDefaultStateToRunning)
            {
                if (moveAmount == 1)
                    sprintModeTimer += Time.deltaTime;
                else
                    sprintModeTimer = 0;
            }
#endif
        }

        public override void HandleUpdate()
        {
            if (playerController.CameraType == FSCameraType.FirstPerson)
                locomotionCore.UseMultiDirectionalAnimations(true);

            if (inputManager.Crouch)
                locomotionCore.SetCrouching(!locomotionCore.IsCrouching);

            if (inputManager.ToggleRun)
                setDefaultStateToRunning = !setDefaultStateToRunning;

            MoveType moveType = setDefaultStateToRunning ? MoveType.Run : MoveType.Walk;

            if (inputManager.SprintKey || sprintModeTimer > 2f)
            {
                if (enableSprint)
                    moveType = MoveType.Sprint;
                else
                    moveType = MoveType.Walk;
            }
            locomotionCore.SetMoveType(moveType);

            if (playerController.CameraType == FSCameraType.FirstPerson)
                locomotionCore.PreventTurnBacks = true;

            Vector3 desiredFacingDir = desiredMoveDir;

            if (locomotionCore.UseMultiDirectionalAnimation)
            {
                var cameraForward = cameraGameObject.transform.forward;
                desiredFacingDir = cameraForward;
            }
            desiredFacingDir.y = 0;

            locomotionCore.HandleLocomotion(desiredMoveDir, 0, desiredFacingDir,
                inputManager.JumpKeyDown, inputManager.Drop);

            if (playerController.CurrentEquippedSystem == null)
                playerController.AlignTargetWithCameraForward = (locomotionCore.UseMultiDirectionalAnimation && faceCameraForwardWhenIdle) || playerController.CameraType == FSCameraType.FirstPerson;
        }

        void GetInput()
        {
            float h = inputManager.DirectionInput.x;
            float v = inputManager.DirectionInput.y;

            moveInput = (new Vector3(h, 0, v));
            moveAmount = moveInput.magnitude;
            desiredMoveDir = playerController.CameraPlanarRotation * moveInput;
            desiredMoveDir = Vector3.ClampMagnitude(desiredMoveDir, 1);
        }


        #region Interface

        public Vector3 MoveDir { get { return desiredMoveDir; } set { desiredMoveDir = value; } }
        public bool IsGrounded => locomotionCore.CheckIsGrounded();
        public bool WaitToStartSystem { get; set; } = false;
        public float Gravity => locomotionCore.gravity;
        public bool PreventAllSystems { get; set; } = false;
        public Animator Animator
        {
            get { return animator == null ? GetComponent<Animator>() : animator; }
            set
            {
                animator = value;
            }
        }
        
        public void FocusSystem(SystemBase system, bool needHandsForAction = false)
        {
            bool focusSuccess = OnFocusSystem(system);
            if (!focusSuccess) return;

            locomotionCore.ResetTargetRotation();
            locomotionCore.ResetCurrentVelocity();

            if (needHandsForAction)
                UnEquipCurrentItem();

            if (system.ResetMoveParametersOnFocus)
                locomotionCore.ResetAnimationMotionParams();

            if(system.ResetColliderSizeOnFocus)
                characterCapsule.ResetColliderSize();
        }

        public void UnFocusSystem(SystemBase system)
        {
            if (system != playerController.FocusedScript) return;
            OnUnFocusSystem(system);
            locomotionCore.ResetTargetRotation();

            locomotionCore.SetCurrentVelocity(0, Vector3.zero, system.ResetMoveParametersOnUnFocus);
            locomotionCore.GroundCheck();

            // Idle animation of an item might be stopped when starting this system, so resume it
            itemEquipper.ResumeIdleAnimation();
            itemEquipper.PreventItemSwitching = false;
        }

        bool OnFocusSystem(SystemBase systemBase)
        {
            if (playerController.FocusedScript != null)
            {
                // CanBeInterrupted veto � the focused system may refuse eviction.
                if (!playerController.FocusedScript.CanBeInterrupted(systemBase))
                    return false;

                if (playerController.CurrentEquippedSystem != playerController.FocusedScript)
                    playerController.FocusedScript.ExitSystem();
                playerController.FocusedScript?.UnFocusScript();
            }

            if (playerController.CurrentEquippedSystem != null && playerController.CurrentEquippedSystem != systemBase)
            {
                playerController.CurrentEquippedSystem.ExitSystem();
            }

            systemBase.EnterSystem();
            systemBase.FocusScript();
            playerController.SetSystemState(systemBase.State);

            return true;
        }

        void OnUnFocusSystem(SystemBase systemBase)
        {
            systemBase?.UnFocusScript();
            systemBase?.ExitSystem();
            playerController.ResetState();
        }

        async void UnEquipCurrentItem()
        {
            WaitToStartSystem = true;
            var equippedItem = itemEquipper.EquippedItem;
            itemEquipper.PreventItemSwitching = true;

            // If equipped item is nulll or if we're not tyring to equip or change an item then return
            if (equippedItem == null && !itemEquipper.IsChangingItem)
            {
                itemEquipper.InterruptItemSwitching = false;
                WaitToStartSystem = false;
                return;
            }

            // If we are changing an item
            //     case 1: If an item is being unequipped, then don't equip the new item
            //     case 2: If an item is being equipped, then wait for it to complete so that we can unequip it later
            if (itemEquipper.IsChangingItem)
            {
                itemEquipper.InterruptItemSwitching = true; // To prevent the new item from being equipped if the equipping has not already started

                while (itemEquipper.IsChangingItem)
                    await Task.Yield();

                equippedItem = itemEquipper.EquippedItem;   // Item might have changed
            }

            //animGraph.StopCurrentNonLoopingAnimation();

            // Some items should be unequipped during actions for others we just have to stop it's idle animation
            if (equippedItem != null && equippedItem.unEquipDuringActions)
            {
                itemEquipper.PreventItemSwitching = false;
                itemEquipper.UnEquipItem(false);
                itemEquipper.PreventItemSwitching = true;
            }
            else if (equippedItem != null)
                itemEquipper.StopIdleAnimation();

            itemEquipper.InterruptItemSwitching = false;
            WaitToStartSystem = false;
        }

        #endregion

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || locomotionCore?.ActionExecutor == null)
                return;

            var sb = new StringBuilder();

            sb.Append("Action: ");
            sb.AppendLine(locomotionCore.ActionExecutor.CurrentClipName ?? "�");

            sb.Append("Focused State: ");
            sb.AppendLine(playerController.FocusedScript != null ? playerController.FocusedScript.State.ToString() : "�");

            var labelPos = transform.position + Vector3.up * 2.2f;
            var labelText = sb.ToString();
            var shadowStyle = new GUIStyle
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.black }
            };
            var labelStyle = new GUIStyle
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0f, 0.9f, 0f) }
            };

            // Draw shadow offset in screen space
            var cam = Camera.current;
            if (cam != null)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(labelPos);
                Vector3 shadowScreen = screenPos + new Vector3(1, -1, 0);
                Vector3 shadowWorld = cam.ScreenToWorldPoint(shadowScreen);
                Handles.Label(shadowWorld, labelText, shadowStyle);
            }
            Handles.Label(labelPos, labelText, labelStyle);
#endif
        }

    }
}
