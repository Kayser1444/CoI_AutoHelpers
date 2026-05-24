using System;
using System.Collections.Generic;
using System.Linq;
using CoI.AutoHelpers.Runtime;
using CoI.AutoHelpers.VanillaAttachments;

namespace CoI.AutoHelpers.Persistence
{
    /// <summary>
    /// Small coordinator for mod-owned save lifecycle work. Consuming mods call
    /// <see cref="BeforeVanillaSave"/> from the game's pre-save hook and
    /// <see cref="AfterVanillaSave"/> from the save-complete hook.
    /// </summary>
    public sealed class ModSaveLifecycle : IRuntimeOwned
    {
        private readonly List<IModSaveLifecycleParticipant> m_participants =
            new List<IModSaveLifecycleParticipant>();

        public VanillaAttachmentManager VanillaAttachments { get; }

        public ModSaveLifecycle()
        {
            VanillaAttachments = new VanillaAttachmentManager();
            RegisterParticipant(VanillaAttachments);
        }

        public IReadOnlyList<IModSaveLifecycleParticipant> Participants => m_participants;

        public T RegisterParticipant<T>(T participant)
            where T : IModSaveLifecycleParticipant
        {
            if (participant == null)
                throw new ArgumentNullException(nameof(participant));

            if (!m_participants.Contains(participant))
                m_participants.Add(participant);

            return participant;
        }

        public bool UnregisterParticipant(IModSaveLifecycleParticipant participant, bool disposeRuntime = true)
        {
            if (participant == null)
                return false;

            bool removed = m_participants.Remove(participant);
            if (removed && disposeRuntime)
                participant.DisposeRuntime();
            return removed;
        }

        public void BeforeVanillaSave()
        {
            foreach (IModSaveLifecycleParticipant participant in m_participants.ToArray())
                participant.BeforeVanillaSave();
        }

        public void AfterVanillaSave()
        {
            foreach (IModSaveLifecycleParticipant participant in m_participants.ToArray())
                participant.AfterVanillaSave();
        }

        public void DisposeRuntime()
        {
            foreach (IModSaveLifecycleParticipant participant in m_participants.AsEnumerable().Reverse().ToArray())
                participant.DisposeRuntime();
        }

        public void Dispose()
        {
            DisposeRuntime();
        }
    }
}
