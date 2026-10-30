using FS_LocomotionSystem;
using UnityEngine;


namespace FS_Core
{
    // Can be used to move a character to a point and rotate
    public class CharacterMotor : MonoBehaviour
    {
        public float moveSpeed = 2f;
        public float rotationSpeed = 360f;

        Vector3 targetPosition;
        Quaternion targetRotation;

        bool moveActive;
        bool rotationActive;

        float originalLocomotionType;

        MotionAnimationDriver motionAnimationDriver;
        private void Awake()
        {
            motionAnimationDriver = GetComponent<MotionAnimationDriver>();
        }

        public void SetTarget(Vector3 position, Quaternion rotation)
        {
            targetPosition = position;
            targetRotation = rotation;
            moveActive = true;
            rotationActive = true;

            motionAnimationDriver.SavePrevPos();
            motionAnimationDriver.SetLocomotionType(1);
        }

        public bool IsComplete(float posThreshold = 0.2f, float rotThreshold = 1f)
        {
            bool posDone = MoveCompleted(posThreshold);
            bool rotDone = RotationCompleted(rotThreshold);
            bool isComplete = posDone && rotDone;

            if (isComplete)
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;

                motionAnimationDriver.ResetParamsSmoothly();
            }

            return isComplete;
        }

        public bool MoveCompleted(float posThreshold = 0.2f)
        {
            Vector3 delta = targetPosition - transform.position;
            delta.y = 0;
            bool posDone = delta.magnitude <= posThreshold;

            return posDone;
        }

        public bool RotationCompleted(float rotThreshold = 1f)
        {
            bool rotDone = Quaternion.Angle(transform.rotation, targetRotation) <= rotThreshold;
            return rotDone;
        }

        public void Tick()
        {
            UpdateRotation();
            UpdateMovement();

            motionAnimationDriver.Tick();
        }

        void UpdateRotation()
        {
            if (!rotationActive)
                return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            if (RotationCompleted())
                rotationActive = false;
        }

        void UpdateMovement()
        {
            if (!moveActive)
                return;

            Vector3 delta = targetPosition - transform.position;
            var y = delta.y;

            if (MoveCompleted())
            {
                moveActive = false;
                return;
            }

            Vector3 move = delta.normalized * moveSpeed * Time.deltaTime;
            transform.position += move;
        }
    }
}
