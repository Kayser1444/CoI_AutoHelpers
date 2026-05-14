using System;
using System.IO;
using System.Reflection;
using Mafi;

namespace CoI.AutoHelpers.Logging
{
    /// <summary>
    /// A thin, prefix-scoped wrapper over the Mafi <see cref="Log"/> API.
    ///
    /// Every message is prefixed with the mod tag so log lines are immediately
    /// attributable in the Mafi log file and Unity console alike.
    ///
    /// Usage:
    /// <code>
    /// private static readonly ModLogger Log = new ModLogger("ATD");
    /// Log.Info("Starting mod initialization.");
    /// Log.Info($"version={version}");
    /// </code>
    /// </summary>
    public sealed class ModLogger
    {
        private readonly string m_prefix;

        /// <summary>
        /// Creates a logger whose messages are prefixed with <c>[<paramref name="modTag"/>]</c>.
        /// </summary>
        public ModLogger(string modTag)
        {
            if (string.IsNullOrWhiteSpace(modTag))
            {
                throw new ArgumentException("Mod tag must be non-empty.", nameof(modTag));
            }

            m_prefix = $"[{modTag}] ";
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
        /// Logs a one-time startup banner containing the mod id, version, and
        /// DLL build timestamp. Produces a single identifiable line at the top
        /// of each game session log.
        ///
        /// Example output:
        /// <c>[ATD] AutoTerrainDesignations v0.4.0 | dll: 2026-05-14 16:30:00 UTC</c>
        /// </summary>
        public void LogStartupBanner(string modId, string manifestVersion, Assembly modAssembly)
        {
            if (string.IsNullOrWhiteSpace(modId))
            {
                throw new ArgumentException("Mod id must be non-empty.", nameof(modId));
            }

            if (string.IsNullOrWhiteSpace(manifestVersion))
            {
                throw new ArgumentException("Manifest version must be non-empty.", nameof(manifestVersion));
            }

            if (modAssembly == null)
            {
                throw new ArgumentNullException(nameof(modAssembly));
            }

            string dllTimestamp = GetDllBuildTimestamp(modAssembly);
            Info($"{modId} v{manifestVersion} | dll: {dllTimestamp}");
        }

        private static string GetDllBuildTimestamp(Assembly assembly)
        {
            try
            {
                string location = assembly.Location;
                if (!string.IsNullOrEmpty(location) && File.Exists(location))
                {
                    DateTime lastWrite = File.GetLastWriteTimeUtc(location);
                    return lastWrite.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
                }
            }
            catch
            {
                // Best-effort: timestamp is diagnostic, not critical.
            }

            return "<unknown>";
        }
    }
}
