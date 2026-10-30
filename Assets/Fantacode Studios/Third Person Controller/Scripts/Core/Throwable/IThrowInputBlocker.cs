namespace FS_Core
{
    /// <summary>
    /// Implemented by components that claim the throw input for themselves (the throw
    /// button is shared with attack), e.g. a finisher that should win the press over
    /// aiming the equipped throwable. <see cref="ThrowableController"/> polls all
    /// implementations on the character and won't enter the aim state while any blocks.
    /// </summary>
    public interface IThrowInputBlocker
    {
        bool BlockThrowInput { get; }
    }
}
