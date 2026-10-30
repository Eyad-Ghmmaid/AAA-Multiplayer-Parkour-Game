using FS_Core;
using FS_LocomotionSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_ParkourSystem
{
    [RequireComponent(typeof(ClimbCore))]
    public class ClimbController : SystemBase, IClimbBridge
    {
        [field: Tooltip("Enables Climbing")]
        public bool enableClimbing = true;
        public float maxDistanceToClimb = 1;

        [SerializeField] bool pressInputToClimb = false;

        public override float Priority => 11;
        public override bool IsUnreachableByGround => true;

        public HangType HangType { get => animator.GetFloat(AnimatorParameters.freeHang) < 0.5f ? HangType.bracedHang : HangType.freeHang; }

        Vector3 obstacleRayOffset = new Vector3(0, -0.3f, 0.2f);

        // --- Redirect properties (state lives in ClimbEngine) ---
        public ClimbPoint currentPoint { get => climbCore.currentPoint; set => climbCore.currentPoint = value; }
        public ClimbPoint previousPoint { get => climbCore.previousPoint; set => climbCore.previousPoint = value; }
        public IKweights rightHand => climbCore.rightHand;
        public IKweights leftHand => climbCore.leftHand;
        public IKweights rightFoot => climbCore.rightFoot;
        public IKweights leftFoot => climbCore.leftFoot;

        Transform currentLedge;

        public bool isFalling { get; set; }
        public float isFallingThreshold { get; set; } = 0.2f;
        public float isFallingTimer { get; set; }

        ParkourInputManager inputManager;
        LocomotionInputManager locomotionInputManager;
        EnvironmentScanner envScanner;
        ICharacter player;
        ParkourController parkourController;
        Animator animator;

        PlayerController playerController;

        ClimbCore climbCore;
        ParkourCore parkourCore;

        // Combo state for DoPredictiveClimb (reset each call via ResetPredictiveClimbState)
        bool doCombo;
        bool doQuickCombo;

        public override SystemState State { get; } = SystemState.Climbing;

        public void OnEnable()
        {
            player = GetComponent<ICharacter>();
            playerController = GetComponent<PlayerController>();
            animator = player.Animator;
            inputManager = GetComponent<ParkourInputManager>();
            locomotionInputManager = GetComponent<LocomotionInputManager>();
            parkourController = GetComponent<ParkourController>();
            envScanner = GetComponent<EnvironmentScanner>();
            climbCore = GetComponent<ClimbCore>();
            parkourCore = GetComponent<ParkourCore>();

            if (climbCore != null)
            {
                climbCore.ParkourBridge = parkourController;
                climbCore.ClimbBridge = this;
            }
        }

        public void OnAnimatorIK(int layerIndex)
        {
            // Unity can invoke Animator IK before Start on a freshly spawned player.
            if (climbCore != null)
                climbCore.OnAnimatorIK(layerIndex);
        }


        private Vector3 getFootIK(Vector3 rayStartPos, Vector3 rayDir)
        {
            return climbCore.GetFootIK(rayStartPos, rayDir);
        }

        public override void HandleFixedUpdate()
        {
            if (enableClimbing)
                HandleClimbUpdate();
        }


        public void HandleClimbUpdate()
        {
            if (!parkourController.IsHanging && !parkourController.InAction)
            {
                previousPoint = null;
                currentPoint = null;

                if (!player.IsGrounded)
                {
                    if ((isFallingTimer += Time.deltaTime) > isFallingThreshold)
                        isFalling = true;
                }

                if (isFalling)
                {
                    isFallingTimer = 0;
                    if (player.IsGrounded)
                    {
                        isFalling = false;
                    }

                    if (locomotionInputManager.JumpHold)
                    {
                        var ledges = Physics.OverlapSphere(transform.position + transform.forward * 0.2f + Vector3.up * 1.5f, 0.4f, envScanner.LedgeLayer);
                        if (ledges.Length > 0)
                        {
                            var newPoint = GetNearestPoint(ledges[0].transform, transform.position + Vector3.up, checkDirection: transform.forward);

                            foreach (var item in ledges)
                            {
                                if (newPoint != null) break;
                                newPoint = GetNearestPoint(item.transform, transform.position + Vector3.up, checkDirection: transform.forward);
                            }
                            if (newPoint == null)
                                return;
                            previousPoint = currentPoint;
                            currentPoint = newPoint;

                            StartCoroutine(ClimbWhenFalling());
                        }
                    }

                }
                else if (locomotionInputManager.Drop && !parkourController.InAction)
                {
                    if (envScanner.DropLedgeCheck(transform.forward, out ClimbLedgeData ledgeData))
                    {
                        currentLedge = ledgeData.ledgeHit.transform;
                        if (DropToLedge(currentLedge, ledgeData.ledgeHit.point, false, obstacleCheck: false)) return;
                    }
                }
            }
            else if (!parkourController.InAction)
            {
                var moveInput = locomotionInputManager.DirectionInput;
                float h = moveInput.x;
                float v = moveInput.y;
                direction = new Vector3(h, v, 0f);
                animator.SetBool(AnimatorParameters.BackJumpMode, false);

                var cam = playerController.cameraGameObject.transform;
                var dir = cam.transform.forward;

                if (envScanner.alwaysUsePlayerForward)
                    dir = transform.forward;

                dir.y = 0f;
                var moveDir = ((locomotionInputManager.DirectionInput.x * cam.right) + (locomotionInputManager.DirectionInput.y * cam.forward)).normalized;

                var angleBWvec = Vector3.SignedAngle(transform.forward, dir, Vector3.down);

                var value = -angleBWvec / 180;

                if (Mathf.Abs(value) < 0.2f) value = 0;
                else
                    animator.SetBool(AnimatorParameters.BackJumpMode, true);

                animator.SetFloat(AnimatorParameters.BackJumpDir, Mathf.Clamp(value, -1, 1), 0.2f, Time.deltaTime * 2);
                if (inputManager.JumpFromHang || (locomotionInputManager.JumpHold && moveDir == Vector3.zero))
                {
                    if (moveDir != Vector3.zero || (locomotionInputManager.JumpHold && moveDir == Vector3.zero))
                    {
                        var checkDir = moveDir == Vector3.zero ? dir : moveDir;

                        checkDir.y = 0;

                        parkourController.HandlePredictiveBackJump(checkDir);
                    }
                }

                // Jump back
                else if (locomotionInputManager.Drop && !parkourController.InAction)
                {
                    if (moveInput == Vector2.zero)
                    {
                        DropFromPoint();
                        return;
                    }
                }

                else if (moveInput != Vector2.zero && !parkourController.InAction)
                {
                    // Climb up from mount point
                    if (moveInput.y > 0.6f && animator.GetCurrentAnimatorStateInfo(0).IsName("HangIdles"))
                    {
                        var hitData = envScanner.ObstacleCheck(forwardOriginOffset: (currentPoint.transform.position.y - transform.position.y) + 0.2f);
                        if ((currentPoint.useManualOptions) || (hitData.forwardHitFound && hitData.heightHitFound && hitData.hasSpace && (hitData.heightHit.point.y - currentPoint.transform.position.y) < 0.2f))
                        {
                            if (!currentPoint.useManualOptions || (currentPoint.useManualOptions && currentPoint.MountPoint))
                            {
                                MountPoint();
                                return;
                            }
                        }
                    }
                    if (moveInput.magnitude > 0.5f)
                    {
                        if (pressInputToClimb && locomotionInputManager.JumpHold && moveInput.y > .6f && moveInput.x == 0 && HangType == HangType.bracedHang)
                        {
                            StartCoroutine(climbCore.DoClimbingAction("Bracedhang Try Jump Up", onComplete: () =>
                            {
                                player.FocusSystem(this);
                            }));
                            parkourController.IsHanging = false;
                            isFalling = true;
                            animator.SetBool(AnimatorParameters.IsGrounded, false);
                            return;
                        }

                        ClimbPoint newPoint;

                        newPoint = closestPoint(moveInput);

                        if (!newPoint) return;

                        currentLedge = currentPoint.transform.parent;

                        var distance = Vector3.Distance(currentPoint.transform.position, newPoint.transform.position);
                        var distanceY = newPoint.transform.position.y - currentPoint.transform.position.y;
                        if (distance < 1f)
                        {
                            if (distanceY > 0.6f && HangType == HangType.freeHang)
                                return;
                        }
                        else
                        {
                            if (distanceY > 0f && HangType == HangType.freeHang)
                                return;
                        }
                        var angleDiff = Vector3.Angle(currentPoint.transform.forward, newPoint.transform.forward);

                        if (angleDiff < 30f && distance < 0.9f)
                        {
                            DoShimmyAction(newPoint);

                        }
                        else if (locomotionInputManager.JumpHold)
                        {
                            DoClimbJumpAction(newPoint, angleDiff);
                        }
                    }
                }
            }
        }

        public IEnumerator ClimbWhenFalling()
        {
            yield return climbCore.ClimbWhenFalling();
        }

        public void DoClimbJumpAction(ClimbPoint newPoint, float angleDiff = 0)
        {
            climbCore.DoClimbJumpAction(newPoint, angleDiff);
        }

        public void DoShimmyAction(ClimbPoint newPoint)
        {
            climbCore.DoShimmyAction(newPoint);
        }
        public void DropFromPoint()
        {
            if (HangType == HangType.bracedHang)
                StartCoroutine(climbCore.DoClimbingAction("JumpFromHang",
               onComplete: () =>
               {
                   player.UnFocusSystem(this);
               }));
            else
                StartCoroutine(climbCore.DoClimbingAction("JumpFromFreeHang",
               onComplete: () =>
               {
                   player.UnFocusSystem(this);
               }));
            parkourController.IsHanging = false;
            isFalling = true;
            animator.SetBool(AnimatorParameters.IsGrounded, false);
        }
        public bool DropToLedge(Transform currentLedge, Vector3 point, bool checkAngle = true, bool obstacleCheck = true, Vector3? checkDirection = null)
        {
            var newPoint = GetNearestPoint(currentLedge, point, checkAngle: checkAngle, obstacleCheck: obstacleCheck, checkDirection: checkDirection);
            if (newPoint == null || !CheckSpaceForClimb(newPoint))
                return false;
            DropToPoint(newPoint);
            return true;
        }
        public bool DropToPoint(ClimbPoint newPoint)
        {
            if (newPoint == null) return false;

            currentPoint = newPoint;

            player.FocusSystem(this, true);

            if (climbCore.CheckWall(currentPoint).Value.isWall)
            {
                animator.SetFloat(AnimatorParameters.freeHang, 0);
                StartCoroutine(climbCore.JumpToLedge(currentPoint.transform, "DropToHang", 0.50f, 0.90f, rotateToLedge: true, matchStart: AvatarTarget.LeftFoot, onComplete: () =>
                {
                    animator.SetFloat(AnimatorParameters.freeHang, 0);
                }));
            }
            else
            {
                animator.SetFloat(AnimatorParameters.freeHang, 1);
                StartCoroutine(climbCore.JumpToLedge(currentPoint.transform, "DropToFreeHang", 0.50f, 0.89f, rotateToLedge: true, matchStart: AvatarTarget.LeftFoot, onComplete: () =>
                {
                    animator.SetFloat(AnimatorParameters.freeHang, 1);
                }));
            }
            return true;
        }

        public void MountPoint()
        {
            parkourController.IsHanging = false;

            if (HangType == HangType.bracedHang)
                StartCoroutine(climbCore.DoClimbingAction("BracedHangClimb",
                    onComplete: () =>
                    {
                        player.UnFocusSystem(this);
                        parkourController.IsHanging = false;
                    }));
            else
                StartCoroutine(climbCore.DoClimbingAction("FreeHangClimb",
                   onComplete: () =>
                   {
                       player.UnFocusSystem(this);
                       parkourController.IsHanging = false;
                   }));
        }
        public bool ClimbToLedge(Transform currentLedge, Vector3 point, string animName = null, bool combo = false, Vector3? checkDirection = null)
        {
            var newPoint = GetNearestPoint(currentLedge, point, checkDirection: checkDirection);

            if (newPoint == null)
                return false;

            ClimbToPoint(newPoint, point, animName, combo, checkDirection);
            return true;
        }
        public bool ClimbToPoint(ClimbPoint newPoint, Vector3 point, string animName = null, bool combo = false, Vector3? checkDirection = null)
        {
            if (newPoint == null)
                return false;

            previousPoint = currentPoint;
            currentPoint = newPoint;

            var distance = (new Vector3(currentPoint.transform.position.x - transform.position.x, 0, currentPoint.transform.position.z - transform.position.z)).magnitude;

            if (climbCore.CheckWall(currentPoint).Value.isWall)
            {
                if (distance > 1.5f || combo)
                {
                    if (parkourController.IsHanging)
                        StartCoroutine(parkourCore.DoPredictiveBackJump(null, currentPoint, 0, combo: combo, climbCore: climbCore, climbBridge: this));
                    else
                        StartCoroutine(parkourCore.DoPredictiveClimb(currentPoint, 0, climbCore, this));
                }
                else if (!parkourController.IsHanging)
                {
                    animator.SetFloat(AnimatorParameters.freeHang, 0);
                    parkourController.IsHanging = true;
                    player.FocusSystem(this, true);
                    StartCoroutine(climbCore.JumpToLedge(currentPoint.transform, "IdleToBracedHang", 0.44f, 0.68f, matchStart: AvatarTarget.RightFoot));
                }
                else currentPoint = previousPoint;
            }
            else
            {
                if (distance > 1.5f || combo)
                {
                    if (parkourController.IsHanging)
                        StartCoroutine(parkourCore.DoPredictiveBackJump(null, currentPoint, 1, combo: combo, climbCore: climbCore, climbBridge: this));
                    else
                        StartCoroutine(parkourCore.DoPredictiveClimb(currentPoint, 1, climbCore, this));
                }
                else if (!parkourController.IsHanging)
                {
                    animator.SetFloat(AnimatorParameters.freeHang, 1);
                    parkourController.IsHanging = true;
                    player.FocusSystem(this, true);
                    StartCoroutine(climbCore.JumpToLedge(currentPoint.transform, "IdleToFreeHang", 0.5f, 0.8f, matchStart: AvatarTarget.RightFoot, onComplete: () =>
                    {
                        animator.SetFloat(AnimatorParameters.freeHang, 1);
                    }));
                }
                else currentPoint = previousPoint;
            }
            return true;
        }

        public void initializeBodyParts(TargetMatchParams matchParams = null, AnimatorHelper _animatorHelper = null, float climbTransition = 0, bool mirror = false)
        {
            climbCore.InitializeBodyParts(matchParams, _animatorHelper, climbTransition, mirror);
        }

        public float handSpacing { get => climbCore.handSpacing; set => climbCore.handSpacing = value; }

        public void initializeStartBodyParts(TargetMatchParams matchParams = null, AnimatorHelper _animatorHelper = null, float climbTransition = 0, bool mirror = false)
        {
            climbCore.InitializeStartBodyParts(matchParams, _animatorHelper, climbTransition, mirror);
        }

        public void initializeEndBodyParts(TargetMatchParams matchParams = null, AnimatorHelper _animatorHelper = null, float climbTransition = 0, bool mirror = false)
        {
            climbCore.InitializeEndBodyParts(matchParams, _animatorHelper, climbTransition, mirror);
        }

        public void DoShimmy(float normalTime, bool mirror = false, bool HangTypeChange = false)
        {
            climbCore.DoShimmy(normalTime, mirror, HangTypeChange);
        }

        WallInfo? CheckWall(ClimbPoint point)
        {
            return climbCore.CheckWall(point);
        }
        public bool RayCastCheck(ClimbPoint prevPoint, ClimbPoint currPoint)
        {
            var previousOffset = prevPoint.transform.position + obstacleRayOffset.y * prevPoint.transform.up + obstacleRayOffset.z * prevPoint.transform.forward;

            var currentOffset = currPoint.transform.position + obstacleRayOffset.y * currPoint.transform.up + obstacleRayOffset.z * currPoint.transform.forward;

            var direction = currPoint.transform.position - prevPoint.transform.position;


            var preDir = previousOffset + Vector3.ProjectOnPlane(direction, prevPoint.transform.forward);
            var curDir = currentOffset + Vector3.ProjectOnPlane(-direction, currPoint.transform.forward);

            var middlePoint = (preDir + curDir) / 2;

            var rayCastAmount = 2;

            for (int i = 0; i < rayCastAmount; i++)
            {
                var offset = (Vector3.down * 0.5f / rayCastAmount) * i;
                previousOffset += offset;
                currentOffset += offset;
                middlePoint += offset;
                if (!Physics.Linecast(previousOffset, middlePoint, (envScanner.ObstacleLayer | envScanner.LedgeLayer)))
                    if (!Physics.Linecast(middlePoint, currentOffset, (envScanner.ObstacleLayer | envScanner.LedgeLayer)))
                        return false;
            }

            return true;
        }
        public bool CheckSpaceForClimb(ClimbPoint point)
        {
            var halfExtends = new Vector3(.3f, .5f, .25f);
            var down = Vector3.down * .65f;
            if (!climbCore.CheckWall(point).Value.isWall)
            {
                down = Vector3.down;
                halfExtends.y = 1;
            }
            var hasSpaceForClimb = !Physics.CheckBox(point.transform.position + point.transform.forward * .6f + down, halfExtends, Quaternion.LookRotation(Vector3.right), envScanner.ObstacleLayer);

            return hasSpaceForClimb && !point.hasOwner;
        }


        public Vector3 GetHandPos(Transform ledge, AvatarTarget hand, Vector3? handOffset = null)
        {
            return climbCore.GetHandPos(ledge, hand, handOffset);
        }
        public override void ExitSystem()
        {
            if (parkourController.IsHanging)
            {
                if (animator.isMatchingTarget)
                {
                    animator.InterruptMatchTarget();
                    animator.Update(Time.deltaTime);
                }
                animator.CrossFade(AnimationNames.FallTree, 0.2f);
                animator.Update(0);
                parkourController.IsHanging = false;
            }
        }


        public ClimbPoint closestPoint(Vector2 input)
        {
            var converted = (input.x * transform.right + input.y * transform.up).normalized;
            if (converted.magnitude == 0)
                return null;
            Collider[] hitColliders;


            hitColliders = Physics.OverlapSphere(currentPoint.transform.position + converted, maxDistanceToClimb, envScanner.LedgeLayer);


            List<ClimbPoint> points = new List<ClimbPoint>();
            foreach (var hitCollider in hitColliders)
            {
                points.AddRange(hitCollider.gameObject.GetComponentsInChildren<ClimbPoint>());

            }

            var origin = currentPoint.transform.position;

            var distance = maxDistanceToClimb * 2;

            List<ClimbPoint> overlapPoint = new List<ClimbPoint>();

            foreach (var point in points)
            {
                Vector3 point2origin = point.transform.position - origin;


                var projected = Vector3.ProjectOnPlane(point2origin, currentPoint.transform.forward);

                var projected2 = Vector3.ProjectOnPlane(converted, currentPoint.transform.forward);

                var projected3 = Vector3.ProjectOnPlane(point2origin, currentPoint.transform.up);

                var projected4 = Vector3.ProjectOnPlane(converted, currentPoint.transform.up);

                var angleX = Vector3.Angle(projected, projected2);


                var angleZ = Vector3.SignedAngle(projected3, projected4, Vector3.up);

                if (Vector3.Angle(currentPoint.transform.forward, point.transform.forward) > 100f) continue;

                if (Vector3.Angle(currentPoint.transform.forward, point.transform.forward) > 45f && point2origin.magnitude > 1f) continue;

                if (angleX < 45f && point2origin.magnitude < distance && point != currentPoint)
                {
                    if (angleX < 40f || overlapPoint.Count > 0)
                    {
                        if (CheckSpaceForClimb(point) && !RayCastCheck(currentPoint, point))
                        {
                            overlapPoint.Insert(0, point);
                            distance = point2origin.magnitude;
                        }

                    }
                }
            }
            if (overlapPoint.Count > 0)
                return overlapPoint[0];
            else
                return null;
        }

        Vector3 direction;
        public ClimbPoint ClosestVirPoint(Vector2 input)
        {
            var converted = (input.x * transform.right + input.y * transform.up).normalized;
            if (converted.magnitude == 0)
                return null;
            Collider[] hitColliders;


            hitColliders = Physics.OverlapSphere(currentPoint.transform.position + converted, 1f, envScanner.LedgeLayer);

            float distance = 2f;
            List<ClimbPoint> points = new List<ClimbPoint>();

            GameObject curPoint = new GameObject("test", typeof(ClimbPoint));

            ClimbPoint nextPoint = null;
            foreach (var hitCollider in hitColliders)
            {
                var point = hitCollider.ClosestPoint(currentPoint.transform.position + converted);
                point += hitCollider.transform.forward * 2f;
                point.y += 2f;
                point = hitCollider.ClosestPoint(point);

                var curDistance = ((currentPoint.transform.position + converted) - point).magnitude;
                if (curDistance < distance && curDistance > 0.2f)
                {
                    nextPoint = curPoint.GetComponent<ClimbPoint>();
                    nextPoint.transform.forward = hitCollider.transform.forward;
                    nextPoint.transform.position = point;
                    distance = ((currentPoint.transform.position + converted) - point).magnitude;
                }
            }
            if (nextPoint)
                return nextPoint;
            return null;
        }


        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Gizmos.color = new Vector4(1, 1, 1, 0.1f);
            if (currentPoint)
                Gizmos.DrawSphere(GetHandPos(currentPoint.transform, AvatarTarget.RightHand), 0.02f);

            var converted = (direction.x * transform.right + direction.y * transform.up).normalized;
            if (!currentPoint)
                return;

            var hitColliders = Physics.OverlapSphere(currentPoint.transform.position + converted, 1f, envScanner.LedgeLayer);


            Gizmos.DrawSphere(currentPoint.transform.position + converted, 1f);



            List<ClimbPoint> points = new List<ClimbPoint>();
            foreach (var hitCollider in hitColliders)
            {
                points.AddRange(hitCollider.gameObject.GetComponentsInChildren<ClimbPoint>());

            }

            var origin = currentPoint.transform.position;
            var distance = 2f;

            List<ClimbPoint> overlapPoint = new List<ClimbPoint>();

            foreach (var point in points)
            {
                Vector3 point2origin = point.transform.position - origin;


                var projected = Vector3.ProjectOnPlane(point2origin, currentPoint.transform.forward);

                var projected2 = Vector3.ProjectOnPlane(converted, currentPoint.transform.forward);

                var projected3 = Vector3.ProjectOnPlane(point2origin, currentPoint.transform.up);

                var projected4 = Vector3.ProjectOnPlane(converted, currentPoint.transform.up);

                var angleX = Vector3.Angle(projected, projected2);

                var angleZ = Vector3.SignedAngle(projected3, projected4, Vector3.up);

                point.gameObject.name = angleX.ToString() + " " + angleZ.ToString();

                if (Vector3.Angle(currentPoint.transform.forward, point.transform.forward) > 100f) continue;

                if (Vector3.Angle(currentPoint.transform.forward, point.transform.forward) > 45f && point2origin.magnitude > 1f) continue;

                if (angleX < 30f && point2origin.magnitude < distance && point != currentPoint)
                {
                    if (angleX < 22.5f || overlapPoint.Count > 0)
                    {
                        if (!RayCastCheck(currentPoint, point))
                        {
                            overlapPoint.Insert(0, point);
                            distance = point2origin.magnitude;
                        }

                    }
                }
            }

            if (overlapPoint.Count > 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(overlapPoint[0].transform.position, 0.1f);
            }

            return;
        }

        //distance to point
        public float GetDistPointToLine(Vector3 origin, Vector3 direction, Vector3 point)
        {
            direction.Normalize();
            Vector3 point2origin = point - origin;
            Vector3 point2closestPointOnLine = point2origin - Vector3.Dot(point2origin, direction) * direction;
            return point2closestPointOnLine.magnitude;
        }


        ClimbPoint GetNearestPoint(Transform ledge, Vector3 hitPoint, bool checkAngle = true, Vector3? checkDirection = null, bool obstacleCheck = true, Vector3? predictiveMaxHeight = null)
        {
            var points = ledge.GetComponentsInChildren<ClimbPoint>();
            checkDirection = !checkDirection.HasValue ? player.MoveDir.normalized : checkDirection.Value;

            if (checkDirection.Value == Vector3.zero) checkDirection = transform.forward;


            ClimbPoint nearestPoint = null;
            float minDistance = 2.0f;
            foreach (var point in points)
            {
                var distance = Vector3.Distance(point.transform.position, hitPoint);
                if (checkAngle)
                {
                    var angle = Vector3.Dot(checkDirection.Value, point.transform.forward);
                    if (angle >= 0)
                        continue;
                }

                var pointForward = point.transform.position + point.transform.forward * 0.2f + Vector3.down * 0.4f;

                if (obstacleCheck)
                {
                    if (!CheckSpaceForClimb(point) || Physics.Linecast(transform.position + Vector3.up, pointForward, envScanner.ObstacleLayer))
                        continue;
                }


                if (distance < minDistance && distance >= 0.03f)
                {
                    minDistance = distance;
                    nearestPoint = point;
                }
            }

            return nearestPoint;
        }

        #region IClimbBridge

        bool IClimbBridge.ShouldContinueAction => IsInFocus;

        void IClimbBridge.OnClimbActionStart(bool needHands) => player.FocusSystem(this, needHands);

        void IClimbBridge.ComputeMirrorJump(ParkourCore core)
        {
            var forward = transform.position + transform.forward;
            var diff = (animator.GetBoneTransform(HumanBodyBones.RightFoot).position - forward).sqrMagnitude
                     - (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position - forward).sqrMagnitude;

            if (Mathf.Abs(diff) < 0.1f)
                core.MirrorJump = !core.MirrorJump;
            else
                core.MirrorJump = diff < 0;
        }

        void IClimbBridge.ResetPredictiveClimbState()
        {
            doCombo = false;
            doQuickCombo = false;
        }

        bool IClimbBridge.HandlePredictiveClimbLanding(ClimbPoint point, float comboTime, float peakTime)
        {
            if (climbCore.currentPoint.transform.parent.CompareTag("SwingableLedge") && comboTime >= peakTime)
            {
                var cam = System.Linq.Enumerable.First(Camera.allCameras, c => c.enabled).transform;
                var dir = (transform.position - cam.position).normalized;
                if (envScanner.alwaysUsePlayerForward) dir = transform.forward;
                dir.y = 0f;
                var angleBWvec = Vector3.SignedAngle(transform.forward, new Vector3(dir.x, 0, dir.z), Vector3.down);

                if (Mathf.Abs(angleBWvec) > 130 || Mathf.Abs(angleBWvec) < 60)
                {
                    if ((doCombo && player.MoveDir != Vector3.zero) || doQuickCombo)
                    {
                        var moveDir = ((locomotionInputManager.DirectionInput.x * cam.right) + (locomotionInputManager.DirectionInput.y * cam.forward)).normalized;
                        dir = doQuickCombo ? dir : moveDir;

                        var jumpData = envScanner.FindPointToJump(dir, 4.5f, 1f, false);
                        if (jumpData != null)
                        {
                            if (jumpData.isClimbable && (!jumpData.jumpPointFound || (jumpData.jumpPointFound && jumpData.climbHitData.point.y > jumpData.rootPosition.y)))
                            {
                                if (jumpData.climbHitData.point.y - transform.position.y < 2f)
                                {
                                    if (ClimbToLedge(jumpData.climbHitData.transform, jumpData.climbHitData.point, combo: true))
                                        return true;
                                }
                            }
                            if (jumpData.jumpPointFound)
                            {
                                StartCoroutine(parkourCore.DoPredictiveBackJump(jumpData, combo: true, climbCore: climbCore, climbBridge: this));
                                return true;
                            }
                        }
                    }
                }
            }
            else if (inputManager.JumpFromHang && HangType == HangType.freeHang) doCombo = true;
            else if (locomotionInputManager.JumpHold && HangType == HangType.freeHang) doQuickCombo = true;

            return false;
        }

        void IClimbBridge.OnBackJumpComplete(bool noChain)
        {
            player.UnFocusSystem(this);
        }

        float IClimbBridge.GetHandSpacing(ClimbPoint point, float defaultSpacing)
        {
            if (point != null && point.useManualOptions)
                return point.handSpacing;
            return climbCore.handSpacing;
        }

        #endregion
    }
}
