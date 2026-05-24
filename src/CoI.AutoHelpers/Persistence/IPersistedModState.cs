namespace CoI.AutoHelpers.Persistence
{
    /// <summary>
    /// Marker for small, helper-owned state models that a consuming mod intends
    /// to serialize explicitly. Runtime attachments should not implement this.
    /// </summary>
    public interface IPersistedModState
    {
        int SchemaVersion { get; }
    }
}
