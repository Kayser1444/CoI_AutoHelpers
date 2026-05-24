namespace CoI.AutoHelpers.VanillaAttachments
{
    /// <summary>
    /// Runtime attachment that must not remain reachable from vanilla save-visible
    /// object graphs while the vanilla save pass is running.
    /// </summary>
    public interface ISaveDetachedVanillaAttachment : IVanillaAttachment
    {
        /// <summary>Short explanation used by diagnostics and code reviewers.</summary>
        string SaveDetachmentReason { get; }
    }
}
