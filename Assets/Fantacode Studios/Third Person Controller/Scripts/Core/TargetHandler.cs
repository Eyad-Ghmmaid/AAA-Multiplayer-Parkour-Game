using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{
    public class TargetHandler : MonoBehaviour
    {
        [Tooltip("Target to engage in combat")]
        [SerializeField] Transform defaultTarget;
        [Tooltip("Target to follow when the AI is in it's ambient behavior")]
        [SerializeField] Transform defaultFollowTarget;

        Transform target;
        public Transform Target {
            get => target;
            private set {
                if (target != value)
                {
                    target = value;
                    OnTargetChanged?.Invoke(target);
                }
            }
        }

        public Transform FollowTarget {get; private set;}

        ITargetValidator targetValidator;
        private void Awake()
        {
            targetValidator = GetComponent<ITargetValidator>();

            if (defaultTarget != null)
                TrySetTarget(defaultTarget);

            if (defaultFollowTarget != null)
                FollowTarget = defaultFollowTarget;
        }

        private void Start()
        {
            
        }

        public bool TrySetTarget(Transform target)
        {
            if (target == null)
            {
                Target = null;
                return true;
            }

            if (targetValidator != null && !targetValidator.IsTargetValid(target))
                return false;

            Target = target;
            return true;
        }

        public void SetTarget(Transform target)
        {
            Target = target;
        }

        public event Action<Transform> OnTargetChanged;
    }
}
