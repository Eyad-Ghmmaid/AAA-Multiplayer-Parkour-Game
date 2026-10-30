using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FS_Core
{
    
    /// <summary>
    /// Aggregates all input manager components on this character into a single
    /// inspector panel. Add this to the player character — the custom editor
    /// discovers all MonoBehaviours with "InputManager" in their name and
    /// displays them as foldout sections with enable/disable toggles.
    ///
    /// Individual input managers are hidden from the Inspector and managed
    /// exclusively through this hub.
    /// </summary>
    public class InputManagerHub : MonoBehaviour
    {
        public List<FSInputManagerBase> GetAllInputManagers()
        {
            return GetComponents<FSInputManagerBase>()
                .Where(manager => manager != null)
                .ToList();
        }

        public T GetInputManager<T>() where T : FSInputManagerBase
        {
            return GetComponent<T>();
        }

        public bool TryGetInputManager<T>(out T manager) where T : FSInputManagerBase
        {
            manager = GetComponent<T>();
            return manager != null;
        }

        public IEnumerable<FSInputBindingInfo> GetAllInputBindings()
        {
            foreach (var manager in GetAllInputManagers())
            {
                foreach (var binding in manager.GetInputBindings())
                    yield return binding;
            }
        }

        public bool TryGetInput(string inputId, out FSInputBindingInfo bindingInfo)
        {
            foreach (var manager in GetAllInputManagers())
            {
                foreach (var binding in manager.GetInputBindings())
                {
                    if (string.Equals(binding.Id, inputId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        bindingInfo = binding;
                        return true;
                    }
                }
            }

            bindingInfo = default;
            return false;
        }

        /// <summary>
        /// Hides all managed input managers from the Inspector.
        /// Called by the custom editor.
        /// </summary>
        public void HideInputManagers()
        {
            foreach (var mgr in GetAllInputManagers())
            {
                mgr.hideFlags |= HideFlags.HideInInspector;
            }
        }

        /// <summary>
        /// Restores visibility of all managed input managers in the Inspector.
        /// Called when the hub is removed.
        /// </summary>
        public void ShowInputManagers()
        {
            foreach (var mgr in GetAllInputManagers())
            {
                mgr.hideFlags &= ~HideFlags.HideInInspector;
            }
        }

        void OnDestroy()
        {
            ShowInputManagers();
        }
    }
}
