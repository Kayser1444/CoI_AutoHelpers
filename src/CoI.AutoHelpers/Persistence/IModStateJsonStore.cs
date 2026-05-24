namespace CoI.AutoHelpers.Persistence
{
    /// <summary>
    /// Stores one mod-owned JSON document in a save-associated location.
    /// Implementations decide where the JSON lives; consuming mods decide what
    /// the JSON means.
    /// </summary>
    public interface IModStateJsonStore
    {
        string StorageKind { get; }

        string StateKey { get; }

        string LoadJson(string defaultJson = "");

        ModStateJsonSaveResult SaveJson(string json);
    }
}
