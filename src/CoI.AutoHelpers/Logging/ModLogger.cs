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
                if (TryGetVanillaConsoleLoggingEnabled(consoleCommands, out bool isEnabled))
                {
                    if (isEnabled)
                    {
                        UnityEngine.Debug.Log($"{m_filterTag} Debug build: also_log_to_console already enabled.");
                        return;
                    }

                    ExecuteAlsoLogToConsole(consoleCommands, m_filterTag);
                    return;
                }

                // Fallback for game versions where ConsoleUi internals have moved.
                const string k_appDomainKey = "CoI.AutoHelpers.ConsoleLoggingActivated";
                if (AppDomain.CurrentDomain.GetData(k_appDomainKey) == null)
                {
                    AppDomain.CurrentDomain.SetData(k_appDomainKey, true);
                    ExecuteAlsoLogToConsole(consoleCommands, m_filterTag);
                }
#endif
            });
        }

        internal static bool TryGetVanillaConsoleLoggingEnabled(GameConsoleCommandsExecutor consoleCommands, out bool enabled)
        {
            enabled = false;

            try
            {
                if (!consoleCommands.Executor.Commands.TryGetValue("also_log_to_console", out GameCommand command))
                    return false;

                object target = command.Target;
                if (target == null)
                    return false;

                FieldInfo? field = target.GetType().GetField(
                    "m_isLoggingToConsole",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null || field.FieldType != typeof(bool))
                    return false;

                object? value = field.GetValue(target);
                if (!(value is bool boolValue))
                    return false;

                enabled = boolValue;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool ExecuteAlsoLogToConsole(GameConsoleCommandsExecutor consoleCommands, string logTag)
        {
            bool enabled = consoleCommands.ExecuteOrSchedule("also_log_to_console true");
            if (enabled)
                UnityEngine.Debug.Log($"{logTag} Debug build: auto-executed also_log_to_console.");
            else
                UnityEngine.Debug.LogWarning($"{logTag} Debug build: failed to auto-execute also_log_to_console.");

            return enabled;
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
