using FS_Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_LocomotionSystem
{
    public class MotionAnimationDriver : MonoBehaviour
    {
        [SerializeField] float maxSpeed = 4;

        Animator animator;

        Vector3 prevPos;
        float originalLocomotionType;

        private void Awake()
        {
            animator = GetComponent<Animator>();

            prevPos = transform.position;
        }

        private void Start()
        {
            originalLocomotionType = animator.GetFloat(AnimatorParameters.locomotionType);
        }

        public void Tick()
        {
            UpdateAnimatorMotionParams();
        }

        public void ResetParams()
        {
            animator.SetFloat(AnimatorParameters.moveAmount, 0f, 0.2f, Time.deltaTime);
            animator.SetFloat(AnimatorParameters.strafeAmount, 0f, 0.2f, Time.deltaTime);

            prevPos = transform.position;
        }

        public void ResetParamsSmoothly(float duration = 1f)
        {
            prevPos = transform.position;

            StartCoroutine(ResetParamsAsync(duration));
        }

        IEnumerator ResetParamsAsync(float duration)
        {
            float timer = 0f;
            while (timer <= duration)
            {   
                animator.SetFloat(AnimatorParameters.moveAmount, 0f, 0.2f, Time.deltaTime);
                animator.SetFloat(AnimatorParameters.strafeAmount, 0f, 0.2f, Time.deltaTime);

                animator.SetFloat(AnimatorParameters.locomotionType, originalLocomotionType, 0.2f, Time.deltaTime);

                timer += Time.deltaTime;

                yield return null;
            }
        }

        public void SetLocomotionType(float locomotionType)
        {
            animator.SetFloat(AnimatorParameters.locomotionType, locomotionType);
        }

        public void SavePrevPos()
        {
            prevPos = transform.position;
        }

        void UpdateAnimatorMotionParams()
        {
            // v = dx / dt
            var deltaPos = transform.position - prevPos;
            var velocity = deltaPos / Time.deltaTime;
            velocity.y = 0;

            float speed = maxSpeed;


            animator.SetFloat(
                AnimatorParameters.moveAmount,
                Vector3.Dot(velocity, transform.forward) / speed,
                0.2f,
                Time.deltaTime
            );
            animator.SetFloat(
               AnimatorParameters.strafeAmount,
                Vector3.Dot(velocity, transform.right) / speed,
                0.2f,
                Time.deltaTime
            );

            prevPos = transform.position;
        }
    }
}
