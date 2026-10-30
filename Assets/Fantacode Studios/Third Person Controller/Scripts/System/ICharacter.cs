using System;
using System.Collections;
using UnityEngine;

namespace FS_Core
{
    public interface ICharacter
    {
        /// <summary>
        /// Gravity for playing acitons
        /// </summary>
        public float Gravity { get; }

        /// <summary>
        /// Animator of the controller
        /// </summary>
        public Animator Animator { get; set; }

        /// <summary>
        /// Return the user's input direction in this property. This is used to determine the direction in which the player should jump/perform action. 
        /// </summary>
        public Vector3 MoveDir { get; }

        /// <summary>
        /// Return true in the property if the player's feet is touching the ground. The actions will only be performed if the player is grounded.
        /// </summary>
        public bool IsGrounded { get; }

        /// <summary>
        /// Return true if you want to prevent the character from performing actions.
        /// You can use this property when the character is performing some other actions like attacking, shooting, reloading, etc.
        /// </summary>
        public bool PreventAllSystems { get; set; }

        /// <summary>
        /// Determines whether the system should wait before starting.
        /// This can be used to delay the execution of a system action until certain conditions are met.
        /// </summary>
        public bool WaitToStartSystem { get; set; }

        /// <summary>
        /// This function will be called for focusing a system. Like performing an attack, parkour action, etc.
        /// From this function you can do things like disabling the collider of the player, resetting the walking or running animation paramater, etc.
        /// </summary>
        void FocusSystem(SystemBase systemBase, bool needHandsForAction = false);

        /// <summary>
        /// This function will be called once an action is completed.
        /// From this function you can do things like enabling the collider of the player.
        /// </summary>
        void UnFocusSystem(SystemBase systemBase);
    }
}
