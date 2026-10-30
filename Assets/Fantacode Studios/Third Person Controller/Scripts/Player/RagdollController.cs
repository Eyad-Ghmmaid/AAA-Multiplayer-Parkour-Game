using UnityEngine;
using System.Collections.Generic;

namespace FS_Core
{
    public class RagdollController : MonoBehaviour
    {
        public Animator animator;
        public Rigidbody[] ragdollRigidbodies;
        public Collider[] ragdollColliders;
        public Transform[] animatedBones; // Corresponds to Animator bones
        public Transform[] physicsBones; // Corresponds to Ragdoll bones
        public float ragdollDuration = 3f; // Time to stay in ragdoll
        public float blendDuration = 0.5f; // Time to blend back to animation

        private bool isRagdoll = false;
        private float ragdollTimer = 0f;
        private Dictionary<Transform, (Vector3, Quaternion)> poseSnapshot;
        private bool blending = false;
        private float blendTimer = 0f;

        void Start()
        {
            // Disable ragdoll at the start
            SetRagdollState(false);
        }

        void Update()
        {
            if (isRagdoll)
            {
                ragdollTimer -= Time.deltaTime;
                if (ragdollTimer <= 0f)
                {
                    StartBlendingToAnimator();
                }
            }

            if (blending)
            {
                BlendToAnimator();
            }
        }

        public void TriggerRagdoll()
        {
            if (!isRagdoll)
            {
                isRagdoll = true;
                ragdollTimer = ragdollDuration;

                // Enable ragdoll
                SetRagdollState(true);

                // Disable animator
                animator.enabled = false;
            }
        }

        private void StartBlendingToAnimator()
        {
            isRagdoll = false;
            blending = true;
            blendTimer = blendDuration;

            // Capture current ragdoll pose
            CaptureRagdollPose();

            // Disable ragdoll physics
            SetRagdollState(false);

            // Align character root to the hips
            AlignRootToHips();

            // Enable animator
            animator.enabled = true;

            // Start in "RagdollRecover" state to apply pose snapshot
            animator.Play("RagdollRecover", 0, 0f);
        }

        private void BlendToAnimator()
        {
            blendTimer -= Time.deltaTime;

            float blendFactor = 1f - Mathf.Clamp01(blendTimer / blendDuration);

            foreach (var kv in poseSnapshot)
            {
                Transform physicsBone = kv.Key;
                Transform animatedBone = GetMatchingAnimatedBone(physicsBone);

                if (animatedBone == null) continue;

                // Blend positions and rotations
                animatedBone.position = Vector3.Lerp(physicsBone.position, animatedBone.position, blendFactor);
                animatedBone.rotation = Quaternion.Slerp(physicsBone.rotation, animatedBone.rotation, blendFactor);
            }

            if (blendTimer <= 0f)
            {
                blending = false;
                // Transition to Get-Up animation
                animator.CrossFade("GetUp", 0.2f, 0);
            }
        }

        private void CaptureRagdollPose()
        {
            poseSnapshot = new Dictionary<Transform, (Vector3, Quaternion)>();
            foreach (var rb in ragdollRigidbodies)
            {
                Transform bone = rb.transform;
                poseSnapshot[bone] = (bone.position, bone.rotation);
            }
        }

        private void SetRagdollState(bool state)
        {
            foreach (var rb in ragdollRigidbodies)
            {
                rb.isKinematic = !state;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            foreach (var collider in ragdollColliders)
            {
                collider.enabled = state;
            }
        }

        private void AlignRootToHips()
        {
            Transform hipBone = ragdollRigidbodies[0].transform; // Assume first bone is hips
            transform.position = hipBone.position;
            transform.rotation = Quaternion.Euler(0, hipBone.rotation.eulerAngles.y, 0);
        }

        private Transform GetMatchingAnimatedBone(Transform physicsBone)
        {
            int index = System.Array.IndexOf(physicsBones, physicsBone);
            return index >= 0 && index < animatedBones.Length ? animatedBones[index] : null;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Bullet"))
            {
                TriggerRagdoll();
            }
        }
    }
}