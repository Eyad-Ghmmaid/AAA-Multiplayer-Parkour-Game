using System;
using System.Collections.Generic;
using UnityEngine;
namespace FS_Core
{
    public class SystemBase : MonoBehaviour
    {
        /// <summary>
        /// Called when the script instance is being loaded.
        /// </summary>
        public virtual void HandleAwake() { }

        /// <summary>
        /// Called on the frame when a script is enabled just before any of the Update methods are called the first time.
        /// </summary>
        public virtual void HandleStart() { }

        /// <summary>
        /// Called every fixed framerate frame, if the MonoBehaviour is enabled.
        /// </summary>
        public virtual void HandleFixedUpdate() { }

        /// <summary>
        /// Called every frame, if the MonoBehaviour is enabled.
        /// </summary>
        public virtual void HandleUpdate() { }

        /// <summary>
        /// Gets or sets the priority of the system. Higher numbers indicate higher priority.
        /// </summary>
        public virtual float Priority { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating whether this system is in focus.
        /// </summary>
        public bool IsInFocus { get; set; }

        /// <summary>
        /// For normal actions like attacks or reactions, we should reset the Animation Movement Parameters since the
        /// character should go to idle after action is over. But for systems like swimming, the movement parameters
        /// should be transferred which switching the system
        /// </summary>
        public virtual bool ResetMoveParametersOnFocus => true;
        public virtual bool ResetMoveParametersOnUnFocus => true;
        public virtual bool ResetColliderSizeOnFocus => true;


        public virtual bool IsUnreachableByGround => false;

        /// <summary>
        /// Gets the state of the system.
        /// </summary>
        [field: SerializeField]
        public virtual SystemState State { get; } = SystemState.Other;

        /// <summary>
        /// Focuses this script, making it the only one to run. All other updates are discarded.
        /// </summary>
        /// <param name="executeStates">If set to <c>true</c>, execute states.</param>
        public void FocusScript() => IsInFocus = true;

        /// <summary>
        /// Unfocuses this script, allowing other updates to run.
        /// </summary>
        public void UnFocusScript() => IsInFocus = false;

        /// <summary>
        /// Called when the system is entered.
        /// </summary>
        public virtual void EnterSystem() { }

        /// <summary>
        /// Called when the system is exited.
        /// </summary>
        public virtual void ExitSystem() { }

        /// <summary>
        /// Gets or sets the action to be called when the state is entered.
        /// </summary>
        public Action OnStateEntered { get; set; }

        /// <summary>
        /// Gets or sets the action to be called when the state is exited.
        /// </summary>
        public Action OnStateExited { get; set; }

        // ─── Improvement 1: Interruptibility veto ────────────────────────────────
        /// <summary>
        /// Called before another system evicts this one from focus.
        /// Return false to block the eviction; the requesting system stays unfocused.
        /// Default: always interruptible.
        /// </summary>
        public virtual bool CanBeInterrupted(SystemBase requestor) => true;

        // ─── Improvement 2: Contextual actions ────────────────────────────
        readonly Dictionary<string, Func<object, bool>> contextualActions = new Dictionary<string, Func<object, bool>>();

        /// <summary>
        /// Register a named handler that other components can invoke via TryContextualAction.
        /// Contextual actions are custom actions that can be executed by certain systems
        /// For example, we can add a contextual action to the climbing system to allow the player to
        /// perform a ledge finisher if an enemy AI is above the ledge on which the player is hanging.
        /// Actions like these require multiple systems, so they cannot be included in a single system
        /// and are added as contextual actions
        /// </summary>
        public void RegisterContextualAction(string key, Func<object, bool> handler)
            => contextualActions[key] = handler;

        /// <summary>
        /// Remove a previously registered contextual-action handler.
        /// </summary>
        public void UnregisterContextualAction(string key)
            => contextualActions.Remove(key);

        /// <summary>
        /// Attempt to dispatch a named contextual action to this system.
        /// Returns true if the handler exists and accepted the request.
        /// </summary>
        public virtual bool TryContextualAction(string key, object context = null)
            => contextualActions.TryGetValue(key, out var handler) && handler(context);

        // ─── Improvement 3: Declared co-running permission ────────────────────────
        /// <summary>
        /// Called each frame while this system is focused. Return true to allow
        /// the system with the given state to also run its HandleUpdate/FixedUpdate.
        /// Example: During a predicitve jump in the parkour system, we should permit systems like GrapplingHook/RopeSwing
        /// Default: nothing else runs while this system is focused.
        /// </summary>
        public virtual bool PermitsWhileFocused(SystemState state) => false;
    }


    public class EquippableSystemBase : SystemBase
    {
        public virtual List<Type> EquippableItems => new List<Type>();
        
        public List<SystemBase> SystemExclusionList => new List<SystemBase>();

        //public bool HasEquippableItem => EquippableItems.Count > 0;
    }
}