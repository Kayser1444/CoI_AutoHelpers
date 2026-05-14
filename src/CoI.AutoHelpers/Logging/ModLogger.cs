using System;
using System.IO;
using System.Reflection;
using Mafi;
using Mafi.Core.Console;
using Mafi.Core.GameLoop;

namespace CoI.AutoHelpers.Logging
{
    /// <summary>
    /// A prefix-scoped wrapper over the Mafi <see cref="Log"/> API that also carries
    /// mod identity (id, version, assembly) so that console-logging setup, the startup
    /// banner, and debug console-mirroring registration require no repeated arguments.
    ///
    /// Construct once in <c>IMod</c>'s instance constructor (after the manifest version
    /// is available), then call the setup helpers in <c>Initialize</c>:
    /// <code>
    /// // In mod constructor:
    /// m_log = new ModLogger("AFD", "AutoForestryDesignations", ModVersion, typeof(AutoForestryDesignationsMod).Assembly);
    ///
    /// // In Initialize:
    /// m_log.EnableConsoleLogging();
    /// m_log.LogStartupBanner();
    /// m_log.RegisterAutoConsoleMirroring(this, resolver.Resolve&lt;IGameLoopEvents&gt;(), resolver.Resolve&lt;GameConsoleCommandsExecutor&gt;());
    /// </code>
    /// </summary>
    public sealed class ModLogger
    {
        private readonly string m_prefix;    // "[AFD] "
        private readonly string m_filterTag; // "[AFD]"
        private readonly string m_modId;
        private readonly string m_manifestVersion;
        private readonly Assembly m_modAssembly;

        /// <summary>
        /// Creates a logger for the given mod. All identity fields are stored and
        /// used by the zero-argument setup helpers.
        /// </summary>
        /// <param name="modTag">Short log tag, e.g. <c>"AFD"</c>. Wrapped in brackets for every log line.</param>
        /// <param name="modId">Human-readable mod name, e.g. <c>"AutoForestryDesignations"</c>. Used in the startup banner.</param>
        /// <param name="manifestVersion">Version string from <c>ModManifest.Version</c>.</param>
        /// <param name="modAssembly">The mod's assembly, used to read the DLL build timestamp.</param>
        public ModLogger(string modTag, string modId, string manifestVersion, Assembly modAssembly)
        {
            if (string.IsNullOrWhiteSpace(modTag))
                throw new ArgumentException("Mod tag must be non-empty.", nameof(modTag));
            if (string.IsNullOrWhiteSpace(modId))
                throw new ArgumentException("Mod id must be non-empty.", nameof(modId));
            if (string.IsNullOrWhiteSpace(manifestVersion))
                throw new ArgumentException("Manifest version must be non-empty.", nameof(manifestVersion));
            if (modAssembly == null)
                throw new ArgumentNullException(nameof(modAssembly));

            m_prefix = $"[{modTag}] ";
            m_filterTag = $"[{modTag}]";
            m_modId = modId;
            m_manifestVersion = manifestVersion;
            m_modAssembly = modAssembly;
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
        /// Logs a one-time startup banner. Call after <see cref="EnableConsoleLogging"/>.
        /// Example output: <c>[AFD] AutoForestryDesignations v0.3.0 | dll: 2026-05-14 16:30:00 UTC</c>
        /// </summary>
        public void LogStartupBanner()
        {
            string dllTimestamp = GetDllBuildTimestamp(m_modAssembly);
            Info($"{m_modId} v{m_manifestVersion} | dll: {dllTimestamp}");
        }

        /// <summary>
        /// Registers a renderer-init callback that emits the startup banner and
        /// (in Debug builds) auto-executes <c>also_log_to_console true</c> so the
        /// banner appears in the in-game console.
        ///
        /// In Release builds the banner is still emitted at renderer-init — after
        /// the game has fully initialised — rather than during <c>Initialize</c>.
        /// </summary>
        public void RegisterAutoConsoleMirroring(object owner, IGameLoopEvents gameLoopEvents, GameConsoleCommandsExecutor consoleCommands)
        {
            gameLoopEvents.RegisterRendererInitState(owner, () =>
            {
#if DEBUG
                bool enabled = consoleCommands.ExecuteOrSchedule("also_log_to_console true");
                if (enabled)
                    UnityEngine.Debug.Log($"{m_filterTag} Debug build: auto-executed also_log_to_console.");
                else
                    UnityEngine.Debug.LogWarning($"{m_filterTag} Debug build: failed to auto-execute also_log_to_console.");
#endif
                LogStartupBanner();
            });
        }

        private static string GetDllBuildTimestamp(Assembly assembly)
        {
            // First preference: attribute embedded at compile time via AssemblyMetadata.
            // Reliable regardless of how the mod loader loads the assembly (byte-array load,
            // shadow copy, etc.).
            try
            {
                foreach (AssemblyMetadataAttribute attr in assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false))
                {
                    if (attr.Key == "BuildTimestamp" && !string.IsNullOrEmpty(attr.Value))
                        return attr.Value + " UTC";
                }
            }
            catch { }

            // Fallback: try to read file last-write time from whatever path the runtime exposes.
            string? path = TryResolveDllPath(assembly);
            if (path != null)
            {
                try
                {
                    DateTime lastWrite = File.GetLastWriteTimeUtc(path);
                    return lastWrite.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
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
