using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FS_Core
{

    public class TriggerEventInvoker : MonoBehaviour
    {
        [Tooltip("Tag to identify the object")]
        public string objectTag = "Player";

        [Tooltip("Event to invoke when an object with given tag enters the trigger")]
        public UnityEvent onEnter;

        [Tooltip("Event to invoke when an object with given tag exits the trigger")]
        public UnityEvent onExit;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(objectTag))
            {
                onEnter?.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(objectTag))
            {
                onExit?.Invoke();
            }
        }
    }
}
