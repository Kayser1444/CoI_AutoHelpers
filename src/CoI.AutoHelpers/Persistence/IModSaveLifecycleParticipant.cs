using CoI.AutoHelpers.Runtime;

namespace CoI.AutoHelpers.Persistence
{
    /// <summary>
    /// Participates in the high-level save lifecycle owned by a consuming mod.
    /// </summary>
    public interface IModSaveLifecycleParticipant : IRuntimeOwned
    {
        /// <summary>Called before vanilla save traversal begins.</summary>
        void BeforeVanillaSave();

        /// <summary>Called after vanilla save traversal has completed.</summary>
        void AfterVanillaSave();
    }
}
