using System;
using System.Collections.Generic;
using System.Linq;
using CoI.AutoHelpers.Persistence;

namespace CoI.AutoHelpers.VanillaAttachments
{
    /// <summary>
    /// Owns helper-created runtime attachments that are visible to vanilla systems
    /// while the game runs, but must be detached during vanilla save traversal.
    /// </summary>
    public sealed class VanillaAttachmentManager : IModSaveLifecycleParticipant
    {
        private readonly List<ISaveDetachedVanillaAttachment> m_saveDetached =
            new List<ISaveDetachedVanillaAttachment>();
        private readonly HashSet<ISaveDetachedVanillaAttachment> m_detachedForSave =
            new HashSet<ISaveDetachedVanillaAttachment>();

        public int Count => m_saveDetached.Count;

        public IReadOnlyList<ISaveDetachedVanillaAttachment> SaveDetachedAttachments => m_saveDetached;

        public T Register<T>(T attachment, bool attachImmediately = true)
            where T : ISaveDetachedVanillaAttachment
        {
            if (attachment == null)
                throw new ArgumentNullException(nameof(attachment));

            if (!m_saveDetached.Contains(attachment))
                m_saveDetached.Add(attachment);

            if (attachImmediately && !attachment.IsAttachedToVanilla)
                attachment.AttachToVanilla();

            return attachment;
        }

        public bool Unregister(ISaveDetachedVanillaAttachment attachment, bool disposeRuntime = true)
        {
            if (attachment == null)
                return false;

            m_detachedForSave.Remove(attachment);
            bool removed = m_saveDetached.Remove(attachment);
            if (removed && disposeRuntime)
                attachment.DisposeRuntime();
            return removed;
        }

        public void BeforeVanillaSave()
        {
            m_detachedForSave.Clear();
            foreach (ISaveDetachedVanillaAttachment attachment in m_saveDetached.ToArray())
            {
                if (!attachment.IsAttachedToVanilla)
                    continue;

                attachment.DetachFromVanilla();
                m_detachedForSave.Add(attachment);
            }
        }

        public void AfterVanillaSave()
        {
            foreach (ISaveDetachedVanillaAttachment attachment in m_detachedForSave.ToArray())
            {
                if (!m_saveDetached.Contains(attachment))
                    continue;

                if (!attachment.IsAttachedToVanilla)
                    attachment.AttachToVanilla();
            }

            m_detachedForSave.Clear();
        }

        public void Clear()
        {
            foreach (ISaveDetachedVanillaAttachment attachment in m_saveDetached.ToArray())
                attachment.DisposeRuntime();

            m_saveDetached.Clear();
            m_detachedForSave.Clear();
        }

        public void DisposeRuntime()
        {
            Clear();
        }

        public void Dispose()
        {
            DisposeRuntime();
        }
    }
}
