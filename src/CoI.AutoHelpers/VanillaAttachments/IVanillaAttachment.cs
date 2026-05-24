using CoI.AutoHelpers.Runtime;

namespace CoI.AutoHelpers.VanillaAttachments
{
    /// <summary>
    /// Helper-owned runtime object that can be attached to, and detached from,
    /// vanilla game systems.
    /// </summary>
    public interface IVanillaAttachment : IRuntimeOwned
    {
        /// <summary>True when the attachment is currently registered with vanilla runtime systems.</summary>
        bool IsAttachedToVanilla { get; }

        /// <summary>Registers this object with the vanilla runtime systems it projects into.</summary>
        void AttachToVanilla();

        /// <summary>Unregisters this object from vanilla runtime systems.</summary>
        void DetachFromVanilla();
    }
}
