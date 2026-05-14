using Mafi.Core.Console;
using Mafi.Core.GameLoop;
using UnityEngine;

namespace CoI.AutoHelpers.Logging
{
    /// <summary>
    /// Debug-only helpers for improving log visibility during development.
    ///
    /// Only active in <c>DEBUG</c> builds. All methods are decorated with
    /// <see cref="System.Diagnostics.ConditionalAttribute"/> so they compile away
    /// to nothing in Release builds at the call site.
    /// </summary>
    public static class ModDebugHelpers
    {
        /// <summary>
        /// Registers a renderer-init callback that auto-executes the
        /// <c>also_log_to_console true</c> game console command.
        ///
        /// This mirrors what ATD and AFD both do manually in their mod
        /// initializers. Call once from <c>IMod.Initialize</c> so that
        /// the in-game console mirrors log output without a manual command
        /// each launch.
        ///
        /// <paramref name="owner"/> must be a non-saveable object; the mod
        /// instance itself is the conventional choice.
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void RegisterAutoConsoleMirroring(
            object owner,
            IGameLoopEvents gameLoopEvents,
            GameConsoleCommandsExecutor consoleCommands,
            string logTag)
        {
            gameLoopEvents.RegisterRendererInitState(owner, () =>
            {
                bool enabled = consoleCommands.ExecuteOrSchedule("also_log_to_console true");
                if (enabled)
                    UnityEngine.Debug.Log($"{logTag} Debug build: auto-executed also_log_to_console.");
                else
                    UnityEngine.Debug.LogWarning($"{logTag} Debug build: failed to auto-execute also_log_to_console.");
            });
        }
    }
}
