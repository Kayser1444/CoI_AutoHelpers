namespace CoI.AutoHelpers.Runtime
{
    /// <summary>
    /// Represents helper-owned runtime state that can be explicitly torn down when
    /// a world unloads, a mod shuts down, or a runtime manager is reset.
    /// </summary>
    public interface IRuntimeOwned
    {
        /// <summary>
        /// Releases runtime-only references and unregisters from any live systems.
        /// Implementations should be safe to call more than once.
        /// </summary>
        void DisposeRuntime();
    }
}
