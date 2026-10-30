using FS_LocomotionSystem;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{
    /// <summary>
    /// Player-facing throwable system. Extends <see cref="EquippableSystemBase"/> so that
    /// <see cref="PlayerController"/> activates it whenever a <see cref="ThrowableItem"/> is equipped.
    ///
    /// Responsibilities:
    /// <list type="bullet">
    ///   <item>Reads player input and drives <see cref="ThrowableHandler"/>.</item>
    ///   <item>Owns trajectory arc and impact-marker UI (LineRenderer created dynamically).</item>
    ///   <item>Handles equip / unequip lifecycle via <see cref="ItemEquipper"/> events.</item>
    ///   <item>Enables multi-directional movement and walk speed while aiming.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(ThrowableHandler))]
    public class ThrowableController : EquippableSystemBase
    {
        // ── Trajectory UI ─────────────────────────────────────────────────────

        [Header("Trajectory Line")]
        [SerializeField] Material trajectoryMaterial;
        [SerializeField] Color trajectoryStartColor = Color.white;
        [SerializeField] Color trajectoryEndColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] float trajectoryStartWidth = 0.05f;
        [SerializeField] float trajectoryEndWidth = 0.02f;

        [Header("Trajectory Settings")]
        [SerializeField] GameObject impactMarkerPrefab;
        [SerializeField] int trajectorySegments = 80;
        [SerializeField] float trajectoryTimeStep = 0.025f;
        [SerializeField] float trajectoryMaxTime = 5f;
        [SerializeField] LayerMask trajectoryMask = ~0;

        // Number of initial points to skip so the line doesn't start inside the character.
        int skipStartPoints = 3;

        // ── EquippableSystemBase ──────────────────────────────────────────────

        public override List<Type> EquippableItems => new() { typeof(ThrowableItem) };
        public override SystemState State => SystemState.Throw;

        // ── Public state ──────────────────────────────────────────────────────

        public Vector3 AimPoint { get; private set; }

        // ── Component references ──────────────────────────────────────────────

        ThrowableHandler handler;
        ItemEquipper itemEquipper;
        PlayerController playerController;
        LocomotionCore locomotionCore;
        LocomotionInputManager inputManager;
        ICharacter player;
        IThrowInputBlocker[] throwInputBlockers;

        // Optional UI shown while aiming / while a fuse-on-aim throwable is cooking.
        // Any component implementing IThrowableHUD can supply it (ShooterUI does),
        // so Core has no hard dependency on the Shooter module.
        IThrowableHUD throwableHUD;
        Coroutine fuseUiCoroutine;

        // Optional cover system (CoverController implements ICoverProvider). Cover blocks
        // locomotion and pins the character to the wall, so the player steps out of cover to
        // aim and throw, then steps back in once the throw is over.
        ICoverProvider coverProvider;
        bool leftCoverForThrow;

        // ── Runtime ───────────────────────────────────────────────────────────

        LineRenderer trajectoryLine;
        GameObject impactMarker;
        bool isActive;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        void Awake()
        {
            handler = GetComponent<ThrowableHandler>();
            itemEquipper = GetComponent<ItemEquipper>();
            playerController = GetComponent<PlayerController>();
            locomotionCore = GetComponent<LocomotionCore>();
            inputManager = GetComponent<LocomotionInputManager>();
            player = GetComponent<ICharacter>();
            coverProvider = GetComponent<ICoverProvider>();
            throwInputBlockers = GetComponents<IThrowInputBlocker>();
        }

        bool ThrowInputBlocked
        {
            get
            {
                foreach (var blocker in throwInputBlockers)
                    if (blocker.BlockThrowInput) return true;
                return false;
            }
        }

        void Start()
        {
            CreateTrajectoryLine();
            SetupImpactMarker();

            itemEquipper.OnEquipComplete += OnItemEquipComplete;
            itemEquipper.OnUnEquip += OnItemUnEquip;

            handler.OnThrowStarted += () =>
            {
                HideTrajectory();
                playerController.SetCustomCameraState?.Invoke(null);
            };

            handler.OnThrowComplete += () =>
            {
                ExitAimState();
            };

            // Hook fuse-on-aim indicator. The HUD lives under the same parent
            // canvas as the rest of the player HUD.
            ResolveThrowableHUD();

            handler.OnFuseStarted += OnFuseStarted;
            handler.OnFuseStopped += OnFuseStopped;
        }

        void ResolveThrowableHUD()
        {
            // Try child first, then parent's children (the standard Game UI canvas
            // lives on a sibling under the player's parent root).
            throwableHUD = GetComponentInChildren<IThrowableHUD>(true);
            if (throwableHUD == null && transform.parent != null)
                throwableHUD = transform.parent.GetComponentInChildren<IThrowableHUD>(true);
        }

        void OnFuseStarted(ThrowableItem item)
        {
            if (throwableHUD == null) return;

            // Show the throwable HUD parent. Only fuse-on-aim throwables reach here,
            // so items without a fuse never raise the HUD.
            var ui = throwableHUD.ThrowableUI;
            if (ui != null) ui.SetActive(true);

            var itemImage = throwableHUD.FuseItemImage;
            if (itemImage != null)
            {
                if (item != null && item.icon != null)
                    itemImage.sprite = item.icon;
                itemImage.enabled = true;
                itemImage.gameObject.SetActive(true);
            }

            if (fuseUiCoroutine != null) StopCoroutine(fuseUiCoroutine);
            fuseUiCoroutine = StartCoroutine(AnimateFuseFill(item));
        }

        void OnFuseStopped()
        {
            if (throwableHUD == null) return;
            if (fuseUiCoroutine != null)
            {
                StopCoroutine(fuseUiCoroutine);
                fuseUiCoroutine = null;
            }
            var itemImage = throwableHUD.FuseItemImage;
            if (itemImage != null)
            {
                itemImage.enabled = false;
                itemImage.gameObject.SetActive(false);
            }
            var fill = throwableHUD.FuseFillImage;
            if (fill != null) fill.fillAmount = 0f;
            var timer = throwableHUD.FuseTimerText;
            if (timer != null) timer.text = string.Empty;

            // The HUD went up with the fuse, so it comes down with it.
            var ui = throwableHUD.ThrowableUI;
            if (ui != null) ui.SetActive(false);
        }

        System.Collections.IEnumerator AnimateFuseFill(ThrowableItem item)
        {
            // Per-item fuse length. Subclasses override FuseDuration to supply their
            // own value (e.g. ThrowableWeapon.explosionDelay); the base is 3s.
            float duration = item != null ? item.FuseDuration : 3f;

            float t = 0f;
            var img = throwableHUD.FuseFillImage;
            var timer = throwableHUD.FuseTimerText;
            while (handler.IsFuseActive && t < duration)
            {
                if (img != null)
                    img.fillAmount = Mathf.Clamp01(t / Mathf.Max(duration, 0.001f));
                if (timer != null)
                    timer.text = Mathf.Max(0f, duration - t).ToString("F1");
                t += Time.deltaTime;
                yield return null;
            }
            if (img != null) img.fillAmount = handler.IsFuseActive ? 1f : 0f;
            if (timer != null) timer.text = handler.IsFuseActive ? "0.0" : string.Empty;
            fuseUiCoroutine = null;
        }


        /// <summary>
        /// Called when another system (parkour, climbing, swimming, cover, ...) takes over the
        /// character. An aim in progress is cancelled outright — the aim loop animation, the
        /// cooking fuse and the trajectory belong to a state the player just left.
        /// The pending cover return is dropped too, since the incoming system owns the character now.
        /// </summary>
        public override void ExitSystem()
        {
            if(handler.IsAiming)
                CancelThrow(returnToCover: false);
        }

        void OnDestroy()
        {
            itemEquipper.OnEquipComplete -= OnItemEquipComplete;
            itemEquipper.OnUnEquip -= OnItemUnEquip;
            if (handler != null)
            {
                handler.OnFuseStarted -= OnFuseStarted;
                handler.OnFuseStopped -= OnFuseStopped;
            }
        }

        // ── EquippableSystemBase overrides ────────────────────────────────────

        public override void HandleUpdate()
        {
            if (!isActive || !handler.HasThrowable) return;

            HandleInput();

            if (handler.IsAiming)
            {
                // Switch between High/Low Toss based on distance to aim point.
                handler.UpdateAimDistance(AimPoint);
                UpdateTrajectoryUI();
            }
        }

        // ── Item equip / unequip ──────────────────────────────────────────────

        void OnItemEquipComplete(EquippableItem item)
        {
            if (item is not ThrowableItem throwable) return;

            isActive = true;
            handler.EquipItem(throwable);
        }

        /// <summary>
        /// Unequipping while aiming (weapon switch, holster, drop) must tear the aim state down
        /// here — once the item is gone <see cref="HandleUpdate"/> early-outs on HasThrowable and
        /// PlayerController clears CurrentEquippedSystem, so the trajectory line and impact marker
        /// would otherwise stay on screen with no one left to hide them.
        /// </summary>
        void OnItemUnEquip()
        {
            isActive = false;

            if (handler.IsAiming)
                CancelThrow();     // Also stops the aim animation / fuse and restores camera + locomotion.
            else
                HideTrajectory();
        }


        // ── Input ─────────────────────────────────────────────────────────────

        void HandleInput()
        {
            if (handler.IsThrowing || inputManager == null) return;

            // Some actions can block throws. For example: With brick, the player might also perform stealth finishers
            // So in such cases, we should not start aiming
            if (inputManager.ItemThrowDown && !handler.IsAiming && !ThrowInputBlocked)
                EnterAimState();

            if (handler.IsAiming)
            {
                UpdateAimPoint();

                if (inputManager.ItemThrowUp)
                    ExecuteThrow();
            }

            if ((inputManager.ItemThrowCancelDown || !inputManager.ItemThrowHold) && handler.IsAiming)
                CancelThrow();
        }

        // ── Aim state ────────────────────────────────────────────────────────

        void EnterAimState()
        {
            // Cover blocks locomotion and holds the character against the wall, so the throw is
            // performed out of cover. ExitAimState puts the player back if the cover is still there.
            leftCoverForThrow = coverProvider != null && coverProvider.IsInCover;
            if (leftCoverForThrow)
                coverProvider.ExitCoverForAction();

            handler.StartAim();

            if (locomotionCore != null)
            {
                locomotionCore.UseMultiDirectionalAnimations(true);
                playerController.AlignTargetToCameraForward();
                // SetMaxSpeed(2) caps movement to walk speed — also blocks run / sprint while aiming.
                locomotionCore.EnableWalkOnly();
                locomotionCore.DisableTurningAnimations();
                locomotionCore.PreventRotation = true;
            }

            // The throwable HUD is not shown here — it is driven by the fuse, so only
            // fuse-on-aim throwables raise it (see OnFuseStarted).

            // Apply aim camera settings if configured on the item.
            var item = handler.CurrentEquippedItem;
            if (item != null && item.useAimCamera)
                playerController.SetCustomCameraState?.Invoke(item.aimCameraSettings);
        }

        /// <param name="returnToCover">
        /// False when the aim state is torn down because another system is taking over the
        /// character — going back into cover would then steal focus from that system.
        /// </param>
        void ExitAimState(bool returnToCover = true)
        {
            HideTrajectory();

            // Reset camera back to default.
            playerController.SetCustomCameraState?.Invoke(null);

            if (locomotionCore != null)
            {
                locomotionCore.ResetUseMultiDirectionalAnimations();
                playerController.RestoreTargetCameraAlignment();
                locomotionCore.DisableWalkOnly();
                locomotionCore.ResetTurningAnimations();
                locomotionCore.PreventRotation = false;
            }

            // Hide the throwable HUD parent.
            var ui = throwableHUD?.ThrowableUI;
            if (ui != null) ui.SetActive(false);

            // Step back into the cover we left for the throw. The provider only does so when
            // the player is still standing against it — otherwise the player stays out of cover.
            if (leftCoverForThrow)
            {
                leftCoverForThrow = false;
                if (returnToCover)
                    coverProvider.ReturnToCoverAfterAction();
            }
        }

        void UpdateAimPoint()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float aimRange = handler.CurrentEquippedItem.maxAimRange;
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

            // Raycast from camera center to find what the player is looking at.
            if (Physics.Raycast(ray, out RaycastHit hit, aimRange, trajectoryMask, QueryTriggerInteraction.Ignore))
            {
                AimPoint = hit.point;
            }
            else
            {
                // Nothing hit — aim at max range along camera direction.
                AimPoint = ray.origin + ray.direction * aimRange;
            }
        }

        void ExecuteThrow()
        {
            handler.Throw(AimPoint);
            // ExitAimState is called by OnThrowComplete after the full throw finishes.
        }

        void CancelThrow(bool returnToCover = true)
        {
            handler.CancelAim();
            ExitAimState(returnToCover);
        }

        // ── Trajectory UI ─────────────────────────────────────────────────────

        void UpdateTrajectoryUI()
        {
            if (trajectoryLine == null) return;

            // Throw origin is the projectile's current position in the character's hand.
            Vector3 throwStartPos = handler.GetThrowOrigin();
            Vector3 velocity = handler.CalculateThrowVelocity(throwStartPos, AimPoint);

            // Predict arc with per-segment collision raycasts.
            var trajectoryPoints = PredictTrajectoryWithCollision(throwStartPos, velocity);

            if (trajectoryPoints != null && trajectoryPoints.Count > skipStartPoints + 1)
            {
                // Remove first N points so the line starts away from the character's body.
                var displayPoints = new List<Vector3>(trajectoryPoints);
                displayPoints.RemoveRange(0, Mathf.Min(skipStartPoints, displayPoints.Count - 1));

                trajectoryLine.positionCount = displayPoints.Count;
                trajectoryLine.SetPositions(displayPoints.ToArray());
                trajectoryLine.enabled = true;

                // Impact point is the last trajectory point (collision or end of arc).
                Vector3 hitPoint = trajectoryPoints[trajectoryPoints.Count - 1];

                // Update impact marker with surface-normal alignment.
                if (impactMarker != null)
                {
                    impactMarker.transform.position = hitPoint;
                    impactMarker.SetActive(true);

                    if (trajectoryPoints.Count > 1)
                    {
                        int lastIdx = trajectoryPoints.Count - 1;
                        Vector3 impactDir = (trajectoryPoints[lastIdx] - trajectoryPoints[lastIdx - 1]).normalized;

                        if (Physics.Raycast(hitPoint - impactDir * 0.1f, impactDir, out RaycastHit normalHit,
                                0.5f, trajectoryMask, QueryTriggerInteraction.Ignore))
                            impactMarker.transform.rotation = Quaternion.FromToRotation(Vector3.up, normalHit.normal);
                        else
                            impactMarker.transform.rotation = Quaternion.identity;
                    }
                }
            }
            else
            {
                HideTrajectory();
            }
        }

        /// <summary>
        /// Simulates the projectile arc using real Rigidbody drag/mass and raycasts each
        /// segment for collision detection. Stops at the first collision.
        /// </summary>
        List<Vector3> PredictTrajectoryWithCollision(Vector3 startPoint, Vector3 initialVelocity)
        {
            var (drag, mass) = handler.GetProjectilePhysics();
            var points = new List<Vector3> { startPoint };

            Vector3 position = startPoint;
            Vector3 velocity = initialVelocity;

            for (int i = 0; i < trajectorySegments; i++)
            {
                Vector3 dragForce = -drag * velocity.magnitude * velocity.normalized;
                Vector3 acceleration = Physics.gravity + dragForce / Mathf.Max(mass, 0.001f);

                Vector3 prevPos = position;
                velocity += acceleration * trajectoryTimeStep;
                position += velocity * trajectoryTimeStep;
                points.Add(position);

                // Raycast along this segment for collision.
                Vector3 seg = position - prevPos;
                float segDist = seg.magnitude;

                if (segDist > 0.001f && Physics.Raycast(prevPos, seg.normalized, out RaycastHit hit,
                        segDist, trajectoryMask, QueryTriggerInteraction.Ignore))
                {
                    points[points.Count - 1] = hit.point;
                    break;
                }

                if (i * trajectoryTimeStep > trajectoryMaxTime)
                    break;
            }

            return points;
        }

        void HideTrajectory()
        {
            if (trajectoryLine != null) trajectoryLine.enabled = false;
            if (impactMarker != null) impactMarker.SetActive(false);
        }

        void CreateTrajectoryLine()
        {
            var go = new GameObject("ThrowableTrajectoryLine");
            go.transform.SetParent(transform, false);

            trajectoryLine = go.AddComponent<LineRenderer>();
            trajectoryLine.useWorldSpace = true;
            trajectoryLine.startColor = trajectoryStartColor;
            trajectoryLine.endColor = trajectoryEndColor;
            trajectoryLine.startWidth = trajectoryStartWidth;
            trajectoryLine.endWidth = trajectoryEndWidth;
            trajectoryLine.positionCount = 0;
            trajectoryLine.enabled = false;

            if (trajectoryMaterial != null)
                trajectoryLine.material = trajectoryMaterial;
            else
                trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
        }

        void SetupImpactMarker()
        {
            if (impactMarker == null && impactMarkerPrefab != null)
            {
                impactMarker = Instantiate(impactMarkerPrefab);
                impactMarker.SetActive(false);
            }
        }
    }
}
