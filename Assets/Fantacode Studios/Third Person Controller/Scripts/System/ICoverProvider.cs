namespace FS_Core
{
    /// <summary>
    /// Implemented by the component that owns the character's cover state
    /// (<c>CoverController</c> in the Cover System module).
    ///
    /// Declared here in Core so systems like <see cref="ThrowableController"/> can step out
    /// of cover for the duration of an action and step back in afterwards, without taking a
    /// hard dependency on the Cover System. Resolve it with <c>GetComponent&lt;ICoverProvider&gt;()</c>
    /// and treat a null result as "no cover system on this character".
    /// </summary>
    public interface ICoverProvider
    {
        /// <summary>True while the character is currently in cover.</summary>
        bool IsInCover { get; }

        /// <summary>
        /// Leaves cover so the character can perform an action that cover would otherwise
        /// block. Does nothing when not in cover.
        /// </summary>
        void ExitCoverForAction();

        /// <summary>
        /// Goes back into cover after the action is over, but only if the character is still
        /// standing against a cover and no other system has taken over meanwhile.
        /// Does nothing otherwise.
        /// </summary>
        void ReturnToCoverAfterAction();
    }
}
