namespace FS_Core
{
    /// <summary>
    /// Implemented by components that make a finisher impossible for the character while they
    /// are active, e.g. carrying an object leaves no hands free for an assassination.
    /// The finisher executor polls all implementations on the character and offers no context
    /// while any blocks, which hides the prompt as well as refusing the press.
    /// </summary>
    public interface IFinisherBlocker
    {
        bool BlockFinisher { get; }
    }
}
