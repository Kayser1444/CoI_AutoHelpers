using Mafi;
using Mafi.Logging;
using UnityEngine;

namespace CoI.AutoHelpers.Logging
{
    /// <summary>
    /// Subscribes to <c>Mafi.Log.LogReceived</c> and mirrors matching entries to the
    /// Unity Debug console so they appear in-game during development.
    ///
    /// Only active in <c>DEBUG</c> builds. All methods are decorated with
    /// <see cref="System.Diagnostics.ConditionalAttribute"/> so they compile away
    /// to nothing in Release builds at the call site.
    ///
    /// The filter string is the same tag used by <see cref="ModLogger"/> — e.g.
    /// <c>"[ATD]"</c>. Only log lines that contain this string are forwarded to
    /// Unity, keeping the Unity console focused on the consuming mod.
    ///
    /// Usage:
    /// <code>
    /// ModConsoleLogger.Enable("[ATD]");
    /// </code>
    /// </summary>
    public static class ModConsoleLogger
    {
        private static bool s_isSubscribed;
        private static string? s_filter;

        /// <summary>
        /// Subscribes to <c>Log.LogReceived</c> and forwards lines containing
        /// <paramref name="logFilter"/> to <c>UnityEngine.Debug</c>.
        /// Subsequent calls with the same filter are idempotent.
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Enable(string logFilter)
        {
            if (s_isSubscribed)
                return;

            s_filter = logFilter;
            Log.LogReceived += OnLogReceived;
            s_isSubscribed = true;
            UnityEngine.Debug.Log($"{logFilter} Console logging enabled.");
        }

        /// <summary>
        /// Unsubscribes from <c>Log.LogReceived</c>. No-op if not subscribed.
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Disable(string logFilter)
        {
            if (!s_isSubscribed)
                return;

            Log.LogReceived -= OnLogReceived;
            s_isSubscribed = false;
            s_filter = null;
            UnityEngine.Debug.Log($"{logFilter} Console logging disabled.");
        }

        private static void OnLogReceived(LogEntry logEntry)
        {
            if (s_filter != null && !logEntry.Message.Contains(s_filter))
                return;

            string formatted = FormatLogEntry(logEntry);
            switch (logEntry.Type)
            {
                case Mafi.Logging.LogType.Warning:
                    UnityEngine.Debug.LogWarning(formatted);
                    break;
                case Mafi.Logging.LogType.Error:
                case Mafi.Logging.LogType.Exception:
                    UnityEngine.Debug.LogError(formatted);
                    break;
                default:
                    UnityEngine.Debug.Log(formatted);
                    break;
            }
        }

        private static string FormatLogEntry(LogEntry logEntry)
        {
            string type = LogTypeToString(logEntry.Type);
            string timestamp = logEntry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
            string threadName = string.IsNullOrEmpty(logEntry.ThreadName)
                ? "---"
                : (logEntry.ThreadName.Length >= 3
                    ? logEntry.ThreadName.Substring(0, 3)
                    : logEntry.ThreadName.PadRight(3));
            return $"[{type}] {timestamp} ~{threadName}: {logEntry.Message}";
        }

        private static string LogTypeToString(Mafi.Logging.LogType logType)
        {
            return logType switch
            {
                Mafi.Logging.LogType.Debug => "DBG",
                Mafi.Logging.LogType.Info => "INF",
                Mafi.Logging.LogType.Warning => "WRN",
                Mafi.Logging.LogType.Error => "ERR",
                Mafi.Logging.LogType.Exception => "EXC",
                Mafi.Logging.LogType.GameProgress => "GPS",
                _ => "UNK",
            };
        }
    }
}
