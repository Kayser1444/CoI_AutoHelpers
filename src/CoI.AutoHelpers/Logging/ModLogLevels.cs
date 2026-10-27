using System;
using System.Threading;

namespace CoI.AutoHelpers.Logging
{
    public enum ModLogLevel { Warning = 0, Info = 1, Debug = 2, Trace = 3 }

    /// <summary>Share one instance across a mod's loggers. Reads are lock-free;
    /// configuration and session overrides are published together across threads.</summary>
    public sealed class ModLogLevels
    {
#if DEBUG
        public const ModLogLevel BuildDefault = ModLogLevel.Debug;
#else
        public const ModLogLevel BuildDefault = ModLogLevel.Info;
#endif
        private sealed class State
        {
            internal readonly string Configured;
            internal readonly ModLogLevel Active;
            internal State(string configured, ModLogLevel active)
            { Configured = configured; Active = active; }
        }
        private readonly object m_lock = new object();
        private State m_state = new State("Default", BuildDefault);
        public ModLogLevel Level => Volatile.Read(ref m_state).Active;
        public string ConfiguredLevel => Volatile.Read(ref m_state).Configured;
        public bool IsEnabled(ModLogLevel level) => Level >= level;
        public string Describe()
        {
            State state = Volatile.Read(ref m_state);
            return $"active={state.Active}, configured={state.Configured}, buildDefault={BuildDefault}";
        }
        public bool TryApplyConfiguredLevel(string? value, out string error)
            => TrySet(value, true, out error);
        public bool TrySetSessionLevel(string? value, out string error)
            => TrySet(value, false, out error);
        private bool TrySet(string? value, bool configure, out string error)
        {
            string? name = value?.Trim();
            bool useDefault = string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase);
            ModLogLevel parsed = BuildDefault;
            // Match names explicitly: Enum.TryParse also accepts numeric strings.
            if (!useDefault)
            {
                bool found = false;
                foreach (ModLogLevel candidate in Enum.GetValues(typeof(ModLogLevel)))
                    if (string.Equals(name, candidate.ToString(), StringComparison.OrdinalIgnoreCase))
                    { parsed = candidate; found = true; break; }
                if (!found) { error = "Use Default, Warning, Info, Debug, or Trace."; return false; }
            }
            lock (m_lock)
            {
                State old = m_state;
                string configured = configure ? (useDefault ? "Default" : parsed.ToString()) : old.Configured;
                if (useDefault)
                    parsed = configured == "Default" ? BuildDefault : (ModLogLevel)Enum.Parse(typeof(ModLogLevel), configured);
                Volatile.Write(ref m_state, new State(configured, parsed));
            }
            error = string.Empty;
            return true;
        }
    }
}
