using UnityEngine;

namespace FS_Core
{
    public class ActionSystemBase : SystemBase
    {
        ICharacter character;
        public override SystemState State => SystemState.Action;

        void Awake()
        {
            character = GetComponent<ICharacter>();
        }

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>
        /// Takes focus, blocking all other systems.
        /// Call from any external system that needs exclusive control.
        /// </summary>
        public void RequestFocus()
        {
            character?.FocusSystem(this);
        }

        /// <summary>
        /// Releases focus, allowing other systems to resume.
        /// </summary>
        public void ReleaseFocus()
        {
            character?.UnFocusSystem(this);
        }
    }
}
