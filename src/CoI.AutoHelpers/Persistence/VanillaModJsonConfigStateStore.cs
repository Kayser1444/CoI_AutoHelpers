using System;
using Mafi.Core.Mods;

namespace CoI.AutoHelpers.Persistence
{
    /// <summary>
    /// Stores one JSON document as a string parameter in vanilla ModJsonConfig.
    /// The consuming mod must define the matching parameter in config.json.
    /// </summary>
    public sealed class VanillaModJsonConfigStateStore : IModStateJsonStore
    {
        public const string Kind = "vanilla-mod-json-config";

        private readonly ModJsonConfig m_jsonConfig;

        public string StorageKind => Kind;

        public string StateKey { get; }

        public VanillaModJsonConfigStateStore(ModJsonConfig jsonConfig, string stateKey)
        {
            m_jsonConfig = jsonConfig ?? throw new ArgumentNullException(nameof(jsonConfig));
            if (string.IsNullOrWhiteSpace(stateKey))
                throw new ArgumentException("State key must not be empty.", nameof(stateKey));

            StateKey = stateKey;
        }

        public string LoadJson(string defaultJson = "")
        {
            return m_jsonConfig.GetString(StateKey, defaultJson ?? string.Empty);
        }

        public ModStateJsonSaveResult SaveJson(string json)
        {
            if (!m_jsonConfig.TrySetValue(StateKey, json ?? string.Empty, out string error))
                return ModStateJsonSaveResult.Failure(StorageKind, StateKey, error);

            return ModStateJsonSaveResult.Success(StorageKind, StateKey);
        }
    }
}
