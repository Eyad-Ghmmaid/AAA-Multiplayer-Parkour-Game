using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using UnityEngine;

namespace FS_Core
{
    public enum SystemState
    {
        Locomotion, Parkour, Climbing, Combat, GrapplingHook, Swing, Shooter, Cover, Zipline, Gliding, Action, Hotspot, Throw, Swim, Sliding, Vehicle, Other
    }

    public enum FSCameraType { FirstPerson, ThirdPerson }

    /// <summary>
    /// SYSTEM BASE ARCHITECTURE
    /// There are two classes SystemBase and EquippableSystemBase (child of SystemBase)
    /// SystemBase is used for normal systems like Locomotion, Parkour, Climbing, etc. They are stored in CoreSystems list.
    /// EquippableSystemBase is used for the systems that are related to the equipped item like Melee, Shooter, Grappling Hook, etc.
    /// When a system is focused, only that system will run and all the other systems will be ignored.
    /// When no system is focused, all the core systems will run and the equipped system (if there is one) will also run.
    /// So a system is only focused when no other system should run.
    /// SystemState is just an enum used to identify a SystemBase
    /// 
    /// EXAMPLE WORKING
    /// When the player equips a melee weapon, the Melee System will be the CurrentEquippedSystem but it won't be focused
    /// So the HandleUpdate of the Melee System will run along with the HandleUpdate of the Core Systems like Locomotion, etc.
    /// But when the character starts to perform an attack with the melee weapon, the Melee System will be focused,
    /// so only the HandleUpdate of the Melee System will run and the HandleUpdate of the Core Systems will be ignored.
    /// </summary>

    [DefaultExecutionOrder(-20)]
    public class PlayerController : MonoBehaviour, ISavable, ITargetable
    {
        public List<SystemBase> managedScripts = new List<SystemBase>();
        public List<SystemBase> CoreSystems { get; set; } = new List<SystemBase>();
        public List<EquippableSystemBase> EquippableSystems { get; set; } = new List<EquippableSystemBase>();

        [field: SerializeField] public SystemState CurrentSystemState { get; private set; }
        public SystemBase FocusedScript => CoreSystems.FirstOrDefault(x => x.IsInFocus) ?? EquippableSystems.FirstOrDefault(x => x.IsInFocus);

        public SystemState PreviousSystemState { get; private set; }
        public SystemState DefaultSystemState => SystemState.Locomotion;
        public SystemState FocusedSystemState => FocusedScript == null ? DefaultSystemState : FocusedScript.State;
        public SystemState CurrentEquippedSystemState => CurrentEquippedSystem.State;

        public FSCameraType CameraType { get; set; } = FSCameraType.ThirdPerson;
        public Action<FSCameraType> OnCameraTypeChanged;

        

        [HideInInspector]
        [SerializeField]
        private string _editorSearchString = string.Empty;

        public Quaternion CameraPlanarRotation => Quaternion.LookRotation(CameraForwardDirection);
        public Vector3 CameraForwardDirection => Vector3.Scale(cameraGameObject.transform.forward, new Vector3(1, 0, 1));
        public bool AlignTargetWithCameraForward { get; set; }
        bool prevAlignTargetWithCameraForward;

        public void StopAlignCameraForward()
        {
            prevAlignTargetWithCameraForward = AlignTargetWithCameraForward;
            AlignTargetWithCameraForward = false;
        }
        public void ResetAlignCameraForward()
        {
            AlignTargetWithCameraForward = prevAlignTargetWithCameraForward;
        }

        public GameObject cameraGameObject;
        public CameraSettings CurrentCameraSettings { get; set; }


        public Animator animator { get; set; }
        ItemEquipper equippableItemController;
        public ICharacter player { get; set; }
        public Damagable Damagable { get; private set; }

        public Action<float, float> OnStartCameraShake;
        public Action<Vector3> CameraLookAtPoint;
        public Action<CameraSettings> SetCustomCameraState;
        public Action<RecoilInfo> CameraRecoil;
        public Action<float, float> OnLand;

        public bool IsInAir { get; set; }
        public bool PreventCameraShake { get; set; } = false;

        public Action OnCameraLateUpdate { get; set; }
        public bool IsDead => Damagable.CurrentHealth <= 0;

        public bool IsItemEquipped => equippableItemController.EquippedItem != null;
        public EquippableSystemBase CurrentEquippedSystem { get; private set; }

        public bool IsUnreachableByGround => FocusedScript != null && FocusedScript.IsUnreachableByGround;

        void HandleCurrentEquippedItemUpdate(params SystemState[] systemState)
        {
            if (systemState.Length == 0 || (CurrentEquippedSystem != null && systemState.Contains(CurrentEquippedSystem.State)))
                CurrentEquippedSystem?.HandleUpdate();
        }

        void HandleCurrentEquippedItemFixedUpdate(params SystemState[] systemState)
        {
            if (systemState.Length == 0 || (CurrentEquippedSystem != null && systemState.Contains(CurrentEquippedSystem.State)))
                CurrentEquippedSystem?.HandleFixedUpdate();
        }

        #region Unity Methods

        private void Awake()
        {
            player = GetComponent<ICharacter>();
            Damagable = GetComponent<Damagable>();
            equippableItemController = GetComponent<ItemEquipper>();
            if(cameraGameObject == null)
                cameraGameObject = Camera.main.gameObject;
            animator = player.Animator;

            EquippableSystems = managedScripts.Where(x => x is EquippableSystemBase).Cast<EquippableSystemBase>().ToList();
            CoreSystems = managedScripts.Where(x => !(x is EquippableSystemBase)).ToList();

            foreach (var script in managedScripts)
            {
                script?.HandleAwake();
            }

            if (equippableItemController != null)
            {
                equippableItemController.OnEquip += (EquippableItem itemData) =>
                {
                    CurrentEquippedSystem = EquippableSystems.FirstOrDefault(m => m.EquippableItems.Any(t => t.IsAssignableFrom(itemData?.GetType())));
                };
                equippableItemController.OnUnEquipComplete += () => { CurrentEquippedSystem = null; };
            }

            var playerLayer = LayerMask.NameToLayer("Player");
            if (gameObject.layer != playerLayer)
                gameObject.layer = playerLayer;

            _alignTargetWithCameraForward = AlignTargetWithCameraForward;
        }

        private void Start()
        {
            foreach (var script in managedScripts)
            {
                script?.HandleStart();
            }
        }

        private void FixedUpdate()
        {
            if (player.PreventAllSystems || Time.timeScale <= 0) return;

            var focusedScript = FocusedScript;

            if (focusedScript)
            {
                focusedScript.HandleFixedUpdate();

                // Can be used to permit other systems while one is focused
                // Example: using grappling hook in the middle of a predictive jump
                if (CurrentEquippedSystem != null && CurrentEquippedSystem != focusedScript
                    && focusedScript.PermitsWhileFocused(CurrentEquippedSystem.State))
                    HandleCurrentEquippedItemFixedUpdate();
            }
            else
            {
                foreach (var script in CoreSystems)
                {
                    if (FocusedScript != null) return;
                    if (script.enabled && script is not EquippableSystemBase)
                        script.HandleFixedUpdate();
                }
                if (FocusedScript == null)
                    CurrentEquippedSystem?.HandleFixedUpdate();
            }
        }

        private void Update()
        {
            if (player.PreventAllSystems) return;
            var focusedScript = FocusedScript;

            if (focusedScript)
            {
                focusedScript.HandleUpdate();

                // Can be used to permit other systems while one is focused
                // Example: using grappling hook in the middle of a predictive jump
                if (CurrentEquippedSystem != null && CurrentEquippedSystem != focusedScript
                    && focusedScript.PermitsWhileFocused(CurrentEquippedSystem.State))
                    HandleCurrentEquippedItemUpdate();
                
            }
            else
            {
                foreach (var script in CoreSystems)
                {
                    if (FocusedScript != null) return;
                    if (script.enabled && script is not EquippableSystemBase)
                        script.HandleUpdate();
                }
                if(FocusedScript == null)
                    CurrentEquippedSystem?.HandleUpdate();
            }
        }

        #endregion

        #region System Management

        

        public void UnfocusAllSystem()
        {
            foreach (var system in managedScripts)
            {
                if (system.IsInFocus)
                    system.UnFocusScript();
            }
        }

        /// <summary>
        /// Forward a contextual-action request to the currently focused system.
        /// Returns true if the focused system handled the request.
        /// </summary>
        public bool TryContextualAction(string key, object context = null)
            => FocusedScript?.TryContextualAction(key, context) ?? false;

        public void SetSystemState(SystemState newState)
        {
            if (CurrentSystemState != newState)
            {
                PreviousSystemState = CurrentSystemState;
                CurrentSystemState = newState;
            }

            // If the state was changed, invoke the appropriate events
            if (PreviousSystemState != CurrentSystemState)
            {
                managedScripts.FirstOrDefault(s => s.State == PreviousSystemState)?.OnStateExited?.Invoke();
                managedScripts.FirstOrDefault(s => s.State == CurrentSystemState)?.OnStateEntered?.Invoke();
            }
        }

        public void ResetState()
        {
            PreviousSystemState = CurrentSystemState; 
            CurrentSystemState = DefaultSystemState; 
        }

        public void PreviousState()
        {
            var prevState = PreviousSystemState;
            prevState = CurrentSystemState;
            CurrentSystemState = prevState;
        }

        #endregion

        #region Register And Unregister Systems

        public void Register(SystemBase script, bool reorderScripts = false)
        {
            if (!managedScripts.Contains(script))
                managedScripts.Add(script);

            if (reorderScripts)
                managedScripts = managedScripts.OrderByDescending(x => x.Priority).ToList();
        }

        public void Unregister(SystemBase script)
        {
            managedScripts.Remove(script);
        }

        #endregion

        [ContextMenu("Find Systems")]
        public void FindSystems()
        {
            managedScripts = GetComponents<SystemBase>().ToList().OrderBy(x => x.Priority).ToList();
        }

        public object CaptureState()
        {
            var saveData = new PlayerSaveData()
            {
                position = new Vector3SaveData(transform.position),
                rotation = new Vector3SaveData(transform.rotation.eulerAngles)
            };

            return saveData;
        }

        public void RestoreState(object state)
        {
            var savedData = (PlayerSaveData)state;
            transform.position = savedData.position.GetVector();
            transform.rotation = savedData.rotation.GetQuaternion();
            Physics.SyncTransforms();
        }

        public Type GetSavaDataType()
        {
            return typeof(PlayerSaveData);
        }

        bool _alignTargetWithCameraForward;
        public void AlignTargetToCameraForward()
        {
            AlignTargetWithCameraForward = true;
        }

        public void RestoreTargetCameraAlignment()
        {
            AlignTargetWithCameraForward = _alignTargetWithCameraForward;
        }
    }


    [Serializable]
    public class RecoilInfo
    {
        [Tooltip("The amount of camera movement caused by recoil. (X = Horizontal, Y = Vertical)")]
        public Vector2 CameraRecoilAmount = new Vector2(0.3f, 2);

        [Tooltip("The total duration of the camera recoil effect, in seconds.")]
        public float CameraRecoilDuration = 0.3f;

        [Tooltip("The percentage of the recoil duration spent reaching the maximum recoil position.")]
        [Range(0.001f, 1)]
        public float recoilPhasePercentage = 0.2f;

        [Tooltip("The minimum possible recoil movement. (X = Horizontal, Y = Vertical)")]
        public Vector2 minRecoilAmount = new Vector2(0.2f, 1);

    }

    [Serializable]
    public class PlayerSaveData
    {
        public Vector3SaveData position;
        public Vector3SaveData rotation;
    }

    [Serializable]
    public class Vector3SaveData
    {
        public float x;
        public float y;
        public float z;

        public Vector3SaveData(Vector3 v)
        {
            this.x = v.x;
            this.y = v.y;
            this.z = v.z;
        }

        public Vector3 GetVector()
        {
            return new Vector3(x, y, z);
        }

        public Quaternion GetQuaternion()
        {
            return Quaternion.Euler(x, y, z);
        }
    }
}