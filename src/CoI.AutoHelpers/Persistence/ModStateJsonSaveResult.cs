using System;

namespace CoI.AutoHelpers.Persistence
{
    /// <summary>Result of writing a mod-owned JSON document to a backing store.</summary>
    public sealed class ModStateJsonSaveResult
    {
        public string StorageKind { get; }

        public string StateKey { get; }

        public bool Succeeded { get; }

        public string ErrorMessage { get; }

        public ModStateJsonSaveResult(string storageKind, string stateKey, bool succeeded, string errorMessage = "")
        {
            if (string.IsNullOrWhiteSpace(storageKind))
                throw new ArgumentException("Storage kind must not be empty.", nameof(storageKind));
            if (string.IsNullOrWhiteSpace(stateKey))
                throw new ArgumentException("State key must not be empty.", nameof(stateKey));

            StorageKind = storageKind;
            StateKey = stateKey;
            Succeeded = succeeded;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public static ModStateJsonSaveResult Success(string storageKind, string stateKey)
        {
            return new ModStateJsonSaveResult(storageKind, stateKey, succeeded: true);
        }

        public static ModStateJsonSaveResult Failure(string storageKind, string stateKey, string errorMessage)
        {
            return new ModStateJsonSaveResult(storageKind, stateKey, succeeded: false, errorMessage);
        }
    }
}
