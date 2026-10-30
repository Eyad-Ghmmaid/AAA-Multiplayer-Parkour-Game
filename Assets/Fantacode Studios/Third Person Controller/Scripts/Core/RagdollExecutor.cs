using FS_Core;
using FS_Util;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FS_Core
{
    public class RagdollExecutor : MonoBehaviour
    {
        //[SerializeField] float minRagdollTime = 3f;
        //[SerializeField] float settledVelocityThreshold = 0.1f;
        //[SerializeField] float blendToAnimatorTime = 0.5f;

        //public List<Rigidbody> ragdollRigidBodies { get; set; }

        //Transform hipsTransform;
        //Animator animator;
        //IMotionDriver motionDriver;
        //bool isRagdollActive;
        //float ragdollTimer;

        //bool isBlending;
        //float blendTimer;
        //Dictionary<Transform, Quaternion> ragdollBoneRotations;
        //Vector3 ragdollHipsPosition;

        //public Action OnRagdollEnd;

        //private void Awake()
        //{
        //    animator = GetComponent<Animator>();

        //    hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);

        //    ragdollRigidBodies = GetRagdollRigidbodies();
        //    motionDriver = GetComponent<IMotionDriver>();
        //}

        //private void Start()
        //{
        //    ActivateRagdoll(false);
        //}

        //public void ActivateRagdoll(bool state)
        //{
        //    if (ragdollRigidBodies.Count > 10)
        //    {
        //        StartCoroutine(ActivateRagdollAsync(state));
        //    }
        //}

        //void FixedUpdate()
        //{
        //    if (!isRagdollActive) return;

        //    ragdollTimer += Time.fixedDeltaTime;
        //    if (ragdollTimer < minRagdollTime) return;

        //    float sqrThreshold = settledVelocityThreshold * settledVelocityThreshold;
        //    foreach (var rb in ragdollRigidBodies)
        //    {
        //        if (rb == null) continue;
        //        if (rb.velocity.sqrMagnitude > sqrThreshold) return;
        //    }

        //    ActivateRagdoll(false);
        //}

        //void LateUpdate()
        //{
        //    if (!isBlending) return;

        //    blendTimer += Time.deltaTime;
        //    float t = Mathf.Clamp01(blendTimer / blendToAnimatorTime);

        //    hipsTransform.position = Vector3.Lerp(ragdollHipsPosition, hipsTransform.position, t);

        //    foreach (var kvp in ragdollBoneRotations)
        //    {
        //        if (kvp.Key == null) continue;
        //        kvp.Key.rotation = Quaternion.Slerp(kvp.Value, kvp.Key.rotation, t);
        //    }

        //    if (t >= 1f)
        //    {
        //        isBlending = false;
        //        OnRagdollEnd?.Invoke();
        //    }
        //}

        //public bool IsFaceDown()
        //{
        //    return Vector3.Dot(hipsTransform.up, Vector3.down) > 0;
        //}

        //IEnumerator ActivateRagdollAsync(bool activate)
        //{
        //    if (activate)
        //    {

        //        // Enable rigidbodies but keep colliders as triggers so no collision
        //        // contacts exist yet — this prevents depenetration impulses from firing
        //        // on the first physics step when bones may be inside walls or the floor.
        //        foreach (var rb in ragdollRigidBodies)
        //        {
        //            if (rb == null) continue;
        //            rb.isKinematic = false;
        //            rb.GetComponent<Collider>().isTrigger = false;
        //        }
        //        yield return new WaitForEndOfFrame();

        //        // Zero all velocities for a clean start — prevents pre-existing character
        //        // motion from compounding with forces when collision is re-enabled.
        //        foreach (var rb in ragdollRigidBodies)
        //        {
        //            if (rb == null) continue;
        //            rb.velocity = Vector3.zero;
        //            rb.angularVelocity = Vector3.zero;
        //        }

        //        yield return null;
        //        animator.enabled = false;

        //        // Re-enable collision — bones are now clear (or nearly clear) of geometry.
        //        //foreach (var rb in ragdollRigidBodies)
        //        //{
        //        //    if (rb == null) continue;
        //        //    rb.GetComponent<Collider>().isTrigger = false;
        //        //}

        //        isRagdollActive = true;
        //        ragdollTimer = 0f;
        //    }
        //    else
        //    {
        //        isRagdollActive = false;

        //        // Capture all bone world rotations for blending
        //        ragdollBoneRotations = new Dictionary<Transform, Quaternion>();
        //        var bonePositions = new Dictionary<Transform, Vector3>();
        //        foreach (var rb in ragdollRigidBodies)
        //        {
        //            if (rb == null) continue;
        //            ragdollBoneRotations[rb.transform] = rb.transform.rotation;
        //            bonePositions[rb.transform] = rb.transform.position;
        //        }

        //        // Snap parent to where the hips landed, raycast down to find ground
        //        var hipsPos = hipsTransform.position;
        //        float groundY = hipsPos.y;
        //        if (Physics.Raycast(hipsPos + Vector3.up * 0.5f, Vector3.down, out var hit, 10f, FSSettings.i.GroundLayer))
        //            groundY = hit.point.y;

        //        PersistentGizmos.DrawSphere(hit.point, 0.3f, Color.red);

        //        var settledPosition = new Vector3(hipsPos.x, groundY, hipsPos.z);
        //        motionDriver.WarpToPosition(settledPosition);

        //        // Restore all bone world positions so the skeleton doesn't shift
        //        foreach (var rb in ragdollRigidBodies)
        //        {
        //            if (rb == null) continue;
        //            rb.transform.position = bonePositions[rb.transform];
        //            rb.transform.rotation = ragdollBoneRotations[rb.transform];
        //        }

        //        DeactivateRigidBodies();

        //        // Re-enable animator and start blending from ragdoll pose
        //        ragdollHipsPosition = hipsTransform.position;
        //        //animator.enabled = true;

        //        //isBlending = true;
        //        //blendTimer = 0f;
        //    }
        //}

        //private void ActivateRigidBodies()
        //{
        //    foreach (Rigidbody rb in ragdollRigidBodies)
        //    {
        //        if (rb == null) continue;
        //        rb.isKinematic = false;
        //        rb.GetComponent<Collider>().isTrigger = false;
        //    }
        //}

        //void DeactivateRigidBodies()
        //{
        //    foreach (Rigidbody rb in ragdollRigidBodies)
        //    {
        //        rb.isKinematic = true;
        //    }
        //}

        //public List<Rigidbody> GetRagdollRigidbodies()
        //{
        //    var animator = transform.GetComponent<Animator>();
        //    var ragdollRigidbodies = new List<Rigidbody>();

        //    if (animator == null || !animator.isHuman)
        //        return ragdollRigidbodies;

        //    // Loop through all HumanBodyBones that can have a Rigidbody in a ragdoll
        //    foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
        //    {
        //        if (bone == HumanBodyBones.LastBone) continue;

        //        Transform boneTransform = animator.GetBoneTransform(bone);
        //        if (boneTransform != null)
        //        {
        //            Rigidbody rb = boneTransform.GetComponent<Rigidbody>();

        //            if (rb != null && !ragdollRigidbodies.Contains(rb))
        //            {
        //                rb.GetComponent<Collider>().isTrigger = false;
        //                ragdollRigidbodies.Add(rb);
        //            }
        //        }
        //    }

        //    return ragdollRigidbodies;
        //}

    }
}
