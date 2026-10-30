namespace FS_Core
{
    /// <summary>
    /// Implemented by components on the character whose current state would be corrupted
    /// by a hit reaction animation (e.g. hanging on a ledge). FighterCore discovers
    /// implementers on the same GameObject and skips the reaction while any returns true —
    /// damage and hit events still go through. The chosen reaction's additive flag is
    /// passed in so implementers can allow layered reactions that don't disturb their state.
    /// </summary>
    public interface IHitReactionSuppressor
    {
        bool SuppressHitReaction(bool isAdditiveReaction);
    }
}
