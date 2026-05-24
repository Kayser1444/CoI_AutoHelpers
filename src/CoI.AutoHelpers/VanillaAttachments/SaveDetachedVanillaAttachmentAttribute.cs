using System;

namespace CoI.AutoHelpers.VanillaAttachments
{
    /// <summary>
    /// Documents a runtime attachment that is deliberately detached before vanilla
    /// save. This is metadata only; the attribute does not hide objects from save.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SaveDetachedVanillaAttachmentAttribute : Attribute
    {
        public string Reason { get; }

        public SaveDetachedVanillaAttachmentAttribute(string reason)
        {
            Reason = reason ?? string.Empty;
        }
    }
}
