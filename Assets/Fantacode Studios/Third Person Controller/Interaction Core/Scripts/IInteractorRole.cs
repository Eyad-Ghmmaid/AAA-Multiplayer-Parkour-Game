namespace FS_InteractionSystem
{
    /// <summary>
    /// The one fact the interaction engine needs about an actor: whether it is AI driven.
    /// Declared here so Interaction Core never references a concrete interactor type — the
    /// interactor implementations live in the Interaction System module, which ships
    /// separately, and the engine must compile without them.
    /// </summary>
    public interface IInteractorRole
    {
        /// <summary>True when the actor is driven by AI rather than by player input.</summary>
        bool IsAIInteractor { get; }
    }
}
