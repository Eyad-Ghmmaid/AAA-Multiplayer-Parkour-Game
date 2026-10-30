using UnityEngine;

namespace FS_Core
{

    public class AttachedItemPhysicsHelper : MonoBehaviour
    {
        [Header("References")]
        public Joint joint;

        private Rigidbody rb;

        [Header("Swing Settings")]
        public float freeSwingAngularDrag = 0.05f;
        public float staticAngularDrag = 5f;

        [Tooltip("Minimum movement delta to count as moving.")]
        public float movementThreshold = 0.001f;

        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private float currentAngularDrag;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (joint == null)
                joint = GetComponent<Joint>();
        }

        private void Start()
        {
            if (joint.connectedBody != null)
            {
                lastPosition = joint.connectedBody.transform.position;
                lastRotation = joint.connectedBody.transform.rotation;
            }
        }

        private void Update()
        {
            if (joint.connectedBody == null) return;

            Transform connectedTransform = joint.connectedBody.transform;

            // Measure position + rotation delta manually
            float posDelta = (connectedTransform.position - lastPosition).sqrMagnitude;
            float rotDelta = Quaternion.Angle(lastRotation, connectedTransform.rotation);

            bool isMoving = posDelta > movementThreshold || rotDelta > 0.01f;

            // Target drag depending on movement state
            float targetDrag = isMoving ? freeSwingAngularDrag : staticAngularDrag;

            // Smooth transition
            currentAngularDrag = Mathf.MoveTowards(currentAngularDrag, targetDrag, Time.deltaTime);

            rb.angularDamping = currentAngularDrag;

            // Store values for next frame
            lastPosition = connectedTransform.position;
            lastRotation = connectedTransform.rotation;
        }
    }
}