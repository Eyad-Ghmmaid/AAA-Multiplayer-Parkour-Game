using FS_LocomotionSystem;
using FS_Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FS_Core
{
    [DefaultExecutionOrder(1)]
    public class CameraController : MonoBehaviour
    {
        // References
        //[Tooltip("Player's transform")]
        //public Transform playerTransform;

        [Tooltip("Target the camera will follow. To override this, set the target in the default settings or through override camera settings.")]
#if UNITY_EDITOR
        [ReadOnly]
#endif
        public Transform followTarget;

        // Camera Type & Default
        [Tooltip("The type of camera. (Example: ThirdPerson, FirstPerson, etc.)")]
        [SerializeField] private FSCameraType CameraType = FSCameraType.ThirdPerson;

        public CameraTypeSettings firstPersonCamera;
        public CameraTypeSettings thirdPersonCamera;

        [Tooltip("Determines whether the cursor should be locked. Must be set before starting play mode. Cannot be changed at runtime.")]
        [SerializeField] private bool lockCursor = true;

        // Collision Settings
        [Tooltip("Layers to check for collision.")]
        [SerializeField] private LayerMask collisionLayers = 1;

        [Tooltip("Padding value to prevent the camera from clipping through obstacles.")]
        [SerializeField] private float collisionPadding = 0.05f;

        [Tooltip("Near clip plane distance. Must be set before starting play mode. Cannot be changed at runtime.")]
        [SerializeField] private float nearClipPlane = 0.01f;

        [Tooltip("Smooth time to use when camera distance is changed due to a collision.")]
        [SerializeField] private float distanceSmoothTimeWhenOcluded = 0.05f;




        float cameraShakeAmount = 0.6f;

        CameraSettings settings;
        CameraSettings customSettings;

        CameraState currentState;
        Vector3 currentFollowPos;
        float targetDistance, currDistance;
        Vector3 currFramingOffset;

        float distSmoothVel = 0f;
        Vector3 framingSmoothVel, followSmoothVel = Vector3.zero;

        float rotationX;
        float rotationY;
        float yRot;

        FSCameraType previousCameraType;

        float invertXVal;
        float invertYVal;

        float targetRotationY;
        float targetRotationX;

        new Camera camera;
        LocomotionInputManager input;
        PlayerController playerController;
        LocomotionController locomotionController;
        CameraTypeSettings currentCameraTypeSettings;

        private void Awake()
        {
            camera = GetComponent<Camera>();
            camera.nearClipPlane = nearClipPlane;

            playerController = followTarget.GetComponent<PlayerController>();
            input = followTarget.GetComponent<LocomotionInputManager>();
            locomotionController = followTarget.GetComponent<LocomotionController>();

            playerController.OnStartCameraShake += StartCameraShake;
            playerController.CameraLookAtPoint += CameraLookAtPoint;
            playerController.OnLand -= playerController.OnStartCameraShake;
            playerController.OnLand += playerController.OnStartCameraShake;

            playerController.SetCustomCameraState += SetCustomCameraState;
            playerController.CameraRecoil += CameraRecoil;
            previousCameraType = CameraType;
        }

        private void Start()
        {
#if !UNITY_ANDROID && !UNITY_IOS
            //if (lockCursor)
            //{
            //    Cursor.visible = false;
            //    Cursor.lockState = CursorLockMode.Locked;
            //}
#endif

            playerController.CameraType = CameraType;
            currentCameraTypeSettings = CameraType == FSCameraType.FirstPerson ? firstPersonCamera : thirdPersonCamera;
            var followTarget = currentCameraTypeSettings.defaultSettings.overridedFollowTarget == null ? this.followTarget : currentCameraTypeSettings.defaultSettings.overridedFollowTarget;
            currentFollowPos = followTarget.position - Vector3.forward * 4;
            settings = currentCameraTypeSettings.defaultSettings;
            currDistance = settings.distance;
            currFramingOffset = settings.framingOffset;
            previousPos = currentFollowPos;
            CalculateNearPlanePoints();

            targetRotationY = transform.rotation.eulerAngles.y;
        }

        List<Vector3> nearPlanePoints = new List<Vector3>();
        void CalculateNearPlanePoints()
        {
            var camera = GetComponent<Camera>();
            float z = camera.nearClipPlane;
            float y = Mathf.Tan((camera.fieldOfView / 2) * Mathf.Deg2Rad) * z;
            float x = y * camera.aspect + collisionPadding;

            nearPlanePoints.Add(new Vector3(x, y, z));
            nearPlanePoints.Add(new Vector3(-x, y, z));
            nearPlanePoints.Add(new Vector3(x, -y, z));
            nearPlanePoints.Add(new Vector3(-x, -y, z));
            nearPlanePoints.Add(Vector3.zero);
        }

        CameraState SystemToCameraState(SystemState state)
        {
            if (state == SystemState.Locomotion)
                return (locomotionController.IsCrouching) ? CameraState.Crouching : CameraState.Locomotion;

            if (Enum.TryParse(state.ToString(), out CameraState result))
                return result;
            return CameraState.Locomotion;
        }

        public void SetCustomCameraState(CameraSettings cameraSettings = null)
        {
            customSettings = cameraSettings;

            if (cameraSettings == null)
            {
                var activeState = (playerController.CurrentEquippedSystem != null) ? playerController.CurrentEquippedSystem.State : playerController.CurrentSystemState;
                var currPlayerState = SystemToCameraState(activeState);

                var overrideSettings = currentCameraTypeSettings.overrideCameraSettings.FirstOrDefault(x => x.state == currPlayerState);
                if (overrideSettings != null)
                    settings = overrideSettings.settings;
                else
                    settings = currentCameraTypeSettings.defaultSettings;
            }
        }

        RecoilInfo GlobalRecoilInfo { get; set; } = new RecoilInfo() { CameraRecoilDuration = 0 };

        public float CameraTotalRecoilDuration { get; set; }

        public void CameraRecoil(RecoilInfo recoilInfo)
        {
            GlobalRecoilInfo.CameraRecoilAmount = Vector3.Scale(UnityEngine.Random.insideUnitCircle, recoilInfo.CameraRecoilAmount);

            if (recoilInfo.minRecoilAmount != Vector2.zero)
            {
                GlobalRecoilInfo.CameraRecoilAmount.x = Mathf.Sign(GlobalRecoilInfo.CameraRecoilAmount.x) * Mathf.Max(Mathf.Abs(GlobalRecoilInfo.CameraRecoilAmount.x), recoilInfo.minRecoilAmount.x);
                GlobalRecoilInfo.CameraRecoilAmount.y = Mathf.Max(Mathf.Abs(GlobalRecoilInfo.CameraRecoilAmount.y), recoilInfo.minRecoilAmount.y);
            }

            CameraTotalRecoilDuration = GlobalRecoilInfo.CameraRecoilDuration = recoilInfo.CameraRecoilDuration;

            GlobalRecoilInfo.recoilPhasePercentage = recoilInfo.recoilPhasePercentage;
        }
        private void LateUpdate()
        {
            playerController.CameraType = CameraType;
            currentCameraTypeSettings = CameraType == FSCameraType.FirstPerson ? firstPersonCamera : thirdPersonCamera;

            if (previousCameraType != CameraType) playerController.OnCameraTypeChanged?.Invoke(CameraType);

            if (Time.timeScale == 0) return;

            var activeState = (playerController.CurrentEquippedSystem != null) ?
                playerController.CurrentEquippedSystem.State : playerController.CurrentSystemState;
            var currPlayerState = SystemToCameraState(activeState);

            if (customSettings != null)
                settings = customSettings;
            else if (currentState != currPlayerState)
            {
                var overrideSettings = currentCameraTypeSettings.overrideCameraSettings.FirstOrDefault(x => x.state == currPlayerState);
                if (overrideSettings != null)
                    settings = overrideSettings.settings;
                else
                    settings = currentCameraTypeSettings.defaultSettings;
            }
            currentState = currPlayerState;

            if (settings == null)
                settings = currentCameraTypeSettings.defaultSettings;

            var followTarget = settings.overridedFollowTarget == null ? currentCameraTypeSettings.defaultSettings.overridedFollowTarget : settings.overridedFollowTarget;
            followTarget = followTarget == null ? this.followTarget : followTarget;

            Quaternion targetRotation;
            if (settings.localRotationOffset != Vector2.zero)
            {
                var targetEuler = followTarget.rotation.eulerAngles;
                var rotationX = targetEuler.x + settings.localRotationOffset.y;
                var rotationY = targetEuler.y + settings.localRotationOffset.x;
                targetRotation = Quaternion.Euler(rotationX, rotationY, 0);
            }
            else
            {
                // Input-based rotation
                invertXVal = (settings.invertX) ? -1 : 1;
                invertYVal = (settings.invertY) ? -1 : 1;

                Vector2 cameraInput = input.CameraInput * settings.sensitivity;
                cameraInput.x *= invertXVal;
                cameraInput.y *= invertYVal;

                // Apply recoil modifications to input instead of target values
                if (GlobalRecoilInfo.CameraRecoilDuration > 0)
                {
                    float normalizedTime = 1 - (GlobalRecoilInfo.CameraRecoilDuration / CameraTotalRecoilDuration);
                    float recoilProgress;

                    if (normalizedTime <= GlobalRecoilInfo.recoilPhasePercentage)
                    {
                        recoilProgress = Time.deltaTime / (CameraTotalRecoilDuration * GlobalRecoilInfo.recoilPhasePercentage);
                        cameraInput.x += GlobalRecoilInfo.CameraRecoilAmount.x * recoilProgress;
                        cameraInput.y -= GlobalRecoilInfo.CameraRecoilAmount.y * recoilProgress;
                    }
                    else
                    {
                        recoilProgress = Time.deltaTime / (CameraTotalRecoilDuration * (1 - GlobalRecoilInfo.recoilPhasePercentage));
                        cameraInput.x -= GlobalRecoilInfo.CameraRecoilAmount.x * recoilProgress;
                        cameraInput.y += GlobalRecoilInfo.CameraRecoilAmount.y * recoilProgress;
                    }

                    GlobalRecoilInfo.CameraRecoilDuration -= Time.deltaTime;
                }

                // Update target rotation angles with input
                targetRotationX += cameraInput.y;
                targetRotationX = Mathf.Clamp(targetRotationX, settings.minVerticalAngle, settings.maxVerticalAngle);
                targetRotationY += cameraInput.x;

                // Use configurable smoothing instead of fixed high-speed lerp

                // Use configurable smoothing instead of fixed high-speed lerp
                float rotationSmoothSpeed = 10f; // Adjust this value as needed (lower = smoother, higher = more responsive)

                rotationX = Mathf.Lerp(rotationX, targetRotationX, Time.deltaTime * rotationSmoothSpeed);
                rotationY = Mathf.Lerp(rotationY, targetRotationY, Time.deltaTime * rotationSmoothSpeed);

                targetRotation = Quaternion.Euler(rotationX, rotationY, 0);
            }
            if (playerController.AlignTargetWithCameraForward)
            {
                this.followTarget.rotation = PlanarRotation;
            }

            // Follow position smoothing
            //currentFollowPos = Vector3.MoveTowards(previousPos, followTarget.position, settings.followSmoothTime * Time.deltaTime * 100);
            currentFollowPos = Vector3.SmoothDamp(previousPos, followTarget.position, ref followSmoothVel, settings.followSmoothTime);

            // Apply camera shake
            if (CameraShakeDuration > 0)
            {
                currentFollowPos += UnityEngine.Random.insideUnitSphere * CurrentCameraShakeAmount * cameraShakeAmount * Mathf.Clamp01(CameraShakeDuration);
                CameraShakeDuration -= Time.deltaTime;
            }

            var targetFramingOffset = settings.framingOffset;
            currFramingOffset = Vector3.SmoothDamp(currFramingOffset, targetFramingOffset, ref framingSmoothVel, currentCameraTypeSettings.framingSmoothTime);
            var forward = targetRotation * Vector3.up;
            forward.y = 0;

            var right = targetRotation * Vector3.right;
            var focusPosition = currentFollowPos + Vector3.up * currFramingOffset.y + forward * currFramingOffset.z;

            bool collisionAdjusted = false;
            if (settings.enableCameraCollisions)
            {
                RaycastHit hit;
                float closestDistance = settings.distance;
                int closestPoint = 0;
                nearPlanePoints[4] = Vector3.zero;
                for (int i = 0; i < nearPlanePoints.Count; i++)
                {
                    if (Physics.Raycast(focusPosition, (transform.TransformPoint(nearPlanePoints[i]) - focusPosition), out hit, (transform.TransformPoint(nearPlanePoints[i]) - focusPosition).magnitude + 0.1f, collisionLayers, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.distance < closestDistance)
                        {
                            closestDistance = hit.distance;
                            closestPoint = i;
                        }
                        collisionAdjusted = true;
                    }
                }
                //(transform.TransformPoint(nearPlanePoints[i]) - focusPosition);
                targetDistance = Mathf.Clamp(settings.distance * (closestDistance / (transform.TransformPoint(nearPlanePoints[closestPoint]) - focusPosition).magnitude), settings.minDistanceFromTarget, closestDistance);
            }
            else
                targetDistance = settings.distance;

            float smoothTime = collisionAdjusted ? distanceSmoothTimeWhenOcluded : currentCameraTypeSettings.distanceSmoothTime;
            currDistance = Mathf.SmoothDamp(currDistance, targetDistance, ref distSmoothVel, smoothTime);

            transform.position = focusPosition - targetRotation * new Vector3(0, 0, currDistance);
            transform.rotation = targetRotation;
            transform.position += transform.right * currFramingOffset.x * (settings.distance == 0 || settings.localRotationOffset != Vector2.zero ? 1 : currDistance / settings.distance);
            previousPos = currentFollowPos;
            previousCameraType = CameraType;
            playerController.OnCameraLateUpdate?.Invoke();

            playerController.CurrentCameraSettings = settings;
        }

        public Quaternion PlanarRotation => Quaternion.Euler(0, rotationY, 0);

        bool moving;
        Vector3 previousPos;
        bool inDelay;
        float cameraRotSmooth;


        public float CameraShakeDuration { get; set; } = 0f;
        public float CurrentCameraShakeAmount { get; set; } = 0f;
        public void StartCameraShake(float currentCameraShakeAmount, float shakeDuration)
        {
            CurrentCameraShakeAmount = currentCameraShakeAmount;
            CameraShakeDuration = shakeDuration;
        }
        public void CameraLookAtPoint(Vector3 worldPoint)
        {
            if (input.CameraInput != Vector2.zero)
                return;

            // 1) Compute direction from camera pos → target point
            Vector3 dir = worldPoint - transform.position;
            if (dir.sqrMagnitude < 0.0001f)
                return; // too close, skip

            // 2) Build a target rotation around global up
            Quaternion lookRot = Quaternion.LookRotation(dir.normalized, Vector3.up);

            // 3) Extract Euler angles
            //    Note: Unity’s Euler X = pitch, Y = yaw
            Vector3 euler = lookRot.eulerAngles;
            float targetPitch = euler.x;
            float targetYaw = euler.y;

            // 4) Clamp pitch to your camera’s allowed vertical range
            //    (optional—use your existing min/max angles)
            targetPitch = Mathf.Clamp(
                // convert from [0..360] to signed (–180..+180) for clamping
                NormalizeAngle(targetPitch),
                settings.minVerticalAngle,
                settings.maxVerticalAngle
            );

            // 5) Store into your internal fields so later smoothing/recoil still works
            rotationX = targetRotationX = targetPitch;
            rotationY = targetRotationY = targetYaw;

            // 6) Apply immediately
            transform.rotation = Quaternion.Euler(targetRotationX, targetRotationY, 0);
        }

        /// <summary>
        /// Converts 0–360 range to –180..+180.
        /// </summary>
        private float NormalizeAngle(float angle)
        {
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        IEnumerator CameraRotDelay()
        {
            var movDist = Vector3.Distance(previousPos, currentCameraTypeSettings.defaultSettings.overridedFollowTarget.transform.position);
            if (movDist > 0.001f)
            {
                if (!moving)
                {
                    moving = true;
                    inDelay = true;
                    yield return new WaitForSeconds(1.5f);
                    inDelay = false;
                }
            }
            else
            {
                moving = false;
                cameraRotSmooth = 0;
            }

            cameraRotSmooth = Mathf.Lerp(cameraRotSmooth, !inDelay ? 25 : 5, Time.deltaTime);
            yRot = Mathf.Lerp(yRot, yRot + input.DirectionInput.x * invertXVal * 2, Time.deltaTime * cameraRotSmooth);
        }


    }


    [System.Serializable]
    public class CameraSettings : ISerializationCallbackReceiver
    {
        public float distance = 2.5f;
        public Vector3 framingOffset = new Vector3(0, 1.5f, 0);
        public float followSmoothTime = 0;

        public Vector2 localRotationOffset;

        [Range(0, 10)]
        public float sensitivity = 0.6f;

        public float minVerticalAngle = -45;
        public float maxVerticalAngle = 70;

        public bool invertX;
        public bool invertY = true;

        public bool enableCameraCollisions = true;
        public float minDistanceFromTarget = 0f;
        public Transform overridedFollowTarget;


        [SerializeField, HideInInspector]
        private bool serialized = false;
        public void OnAfterDeserialize()
        {
            if (serialized == false)
            {
                distance = 2.5f;
                framingOffset = new Vector3(0, 1.5f, 0);
                sensitivity = 0.6f;
                minVerticalAngle = -45;
                maxVerticalAngle = 70;
                invertY = true;
                enableCameraCollisions = true;
            }
        }

        public void OnBeforeSerialize()
        {
            if (serialized)
                return;

            serialized = true;
        }
    }

    [System.Serializable]
    public class OverrideSettings
    {
        public CameraState state;
        public CameraSettings settings;
    }

    [System.Serializable]
    public class CameraTypeSettings
    {
        [Tooltip("Default settings of the camera. You can override these settings based on the state of the player.")]
        public CameraSettings defaultSettings;
        [Tooltip("List of override camera settings for different states.")]
        public List<OverrideSettings> overrideCameraSettings;
        [Tooltip("Smooth time to use when camera distance is changed.")]
        public float distanceSmoothTime = 0;
        [Tooltip("Smooth time to use when the framing offset is changed.")]
        public float framingSmoothTime = 0;
    }

    // This should be in the same order as SystemState
    public enum CameraState
    {
        Locomotion,
        Parkour,
        Climbing,
        Combat,
        GrapplingHook,
        Swing,
        Other,
        Crouching,
        Shooter,
        Cover,
        Swim,
        Sliding
    }
}