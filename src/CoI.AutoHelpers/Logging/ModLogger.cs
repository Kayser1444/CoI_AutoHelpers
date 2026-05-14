using System;
using System.IO;
using System.Reflection;
using Mafi;
using Mafi.Core.Console;
using Mafi.Core.GameLoop;

namespace CoI.AutoHelpers.Logging
{
    /// <summary>
    /// A prefix-scoped wrapper over the Mafi <see cref="Log"/> API.
    ///
    /// Construct once with the mod's short tag, then call the setup helpers in <c>Initialize</c>:
    /// <code>
    /// // In mod constructor or static field initializer:
    /// m_log = new ModLogger("AFD");
    ///
    /// // In Initialize:
    /// m_log.EnableConsoleLogging();
    /// m_log.RegisterAutoConsoleMirroring(this, resolver.Resolve&lt;IGameLoopEvents&gt;(), resolver.Resolve&lt;GameConsoleCommandsExecutor&gt;());
    ///
    /// // In your RegisterRendererInitState callback — announce version and dll:
    /// m_log.Info($"AutoForestryDesignations v{ModVersion} | dll: {ModLogger.GetDllBuildTimestamp(typeof(AutoForestryDesignationsMod).Assembly)}");
    /// </code>
    /// </summary>
    public sealed class ModLogger
    {
        private readonly string m_prefix;    // "[AFD] "
        private readonly string m_filterTag; // "[AFD]"

        /// <summary>
        /// Creates a logger for the given mod tag.
        /// </summary>
        /// <param name="modTag">Short log tag, e.g. <c>"AFD"</c>. Wrapped in brackets for every log line.</param>
        public ModLogger(string modTag)
        {
            if (string.IsNullOrWhiteSpace(modTag))
                throw new ArgumentException("Mod tag must be non-empty.", nameof(modTag));

            m_prefix = $"[{modTag}] ";
            m_filterTag = $"[{modTag}]";
        }

        /// <summary>Logs an info message.</summary>
        public void Info(string message) => Log.Info(m_prefix + message);

        /// <summary>Logs a warning.</summary>
        public void Warning(string message) => Log.Warning(m_prefix + message);

        /// <summary>Logs an error.</summary>
        public void Error(string message) => Log.Error(m_prefix + message);

        /// <summary>Logs an exception with an explanatory context message.</summary>
        public void Exception(Exception ex, string context) => Log.Exception(ex, m_prefix + context);

        /// <summary>
        /// Subscribes to <c>Log.LogReceived</c> and mirrors log lines tagged with this
        /// mod's filter to <c>UnityEngine.Debug</c>. No-op in Release builds.
        /// Must be called before any logging to ensure the startup banner is visible
        /// in the Unity console.
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        public void EnableConsoleLogging() => ModConsoleLogger.Enable(m_filterTag);

        /// <summary>
        /// Registers a renderer-init callback that (in Debug builds) auto-executes
        /// <c>also_log_to_console true</c> so tagged log lines appear in the in-game console.
        /// Uses an <see cref="AppDomain"/>-scoped flag so only the first mod to call this
        /// executes the command, preventing the toggle from flipping off when multiple mods load.
        /// </summary>
        public void RegisterAutoConsoleMirroring(object owner, IGameLoopEvents gameLoopEvents, GameConsoleCommandsExecutor consoleCommands)
        {
            gameLoopEvents.RegisterRendererInitState(owner, () =>
            {
#if DEBUG
                // also_log_to_console is a pure toggle with no boolean semantics — calling it
                // a second time (from another mod) would flip console logging back off.
                // AppDomain.CurrentDomain is shared across all assemblies in the same process,
                // so this flag prevents any mod beyond the first from executing the command.
                const string k_appDomainKey = "CoI.AutoHelpers.ConsoleLoggingActivated";
                if (AppDomain.CurrentDomain.GetData(k_appDomainKey) == null)
                {
                    AppDomain.CurrentDomain.SetData(k_appDomainKey, true);
                    bool enabled = consoleCommands.ExecuteOrSchedule("also_log_to_console true");
                    if (enabled)
                        UnityEngine.Debug.Log($"{m_filterTag} Debug build: auto-executed also_log_to_console.");
                    else
                        UnityEngine.Debug.LogWarning($"{m_filterTag} Debug build: failed to auto-execute also_log_to_console.");
                }
#endif
            });
        }

        public static string GetDllBuildTimestamp(Assembly assembly)
        {
            // First preference: attribute embedded at compile time via AssemblyMetadata.
            // Reliable regardless of how the mod loader loads the assembly (byte-array load,
            // shadow copy, etc.).
            try
            {
                foreach (AssemblyMetadataAttribute attr in assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false))
                {
                    if (attr.Key == "BuildTimestamp" && !string.IsNullOrEmpty(attr.Value))
                        return attr.Value;
                }
            }
            catch { }

            // Fallback: try to read file last-write time from whatever path the runtime exposes.
            string? path = TryResolveDllPath(assembly);
            if (path != null)
            {
                try
                {
                    DateTime lastWrite = File.GetLastWriteTime(path);
                    return lastWrite.ToString("yyyy-MM-dd HH:mm:ss");
                }
                catch
                {
                    // Fall through to version fallback.
                }
            }

            // Fallback: assembly version is always available.
            try
            {
                Version? v = assembly.GetName().Version;
                if (v != null)
                    return $"asm-ver:{v}";
            }
            catch { }

            return "<unknown>";
        }

        private static string? TryResolveDllPath(Assembly assembly)
        {
            // 1. assembly.Location — works when loaded via Assembly.LoadFrom.
            try
            {
                string loc = assembly.Location;
                if (!string.IsNullOrEmpty(loc) && File.Exists(loc))
                    return loc;
            }
            catch { }

            // 2. ManifestModule.FullyQualifiedName — same source, different API.
            try
            {
                string fqn = assembly.ManifestModule.FullyQualifiedName;
                if (!string.IsNullOrEmpty(fqn) && File.Exists(fqn))
                    return fqn;
            }
            catch { }

            // 3. CodeBase — deprecated but preserved in .NET 4.8; may differ from Location
            //    when shadow-copying is active.
            try
            {
                string cb = assembly.CodeBase;
                if (!string.IsNullOrEmpty(cb))
                {
                    string local = new Uri(cb).LocalPath;
                    if (File.Exists(local))
                        return local;
                }
            }
            catch { }

            return null;
        }
    }
}
