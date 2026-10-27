using System;
using System.Diagnostics;
using Mafi;
using Mafi.Core.GameLoop;
// PRE_4_3_COMPAT: retire API-version conditions after Update 5 or support ends; keep DEBUG guards.
#if DEBUG && COI_UPDATE_4_3
using Mafi.Core.WebApi;
#endif

namespace CoI.AutoHelpers.Logging
{
    /// <summary>Starts the native local web API for each Debug game without changing machine settings.</summary>
    public static class ModDebugWebApi
    {
        /// <summary>
        /// Call from IMod.Initialize. Starts after renderer initialization, at the next sync,
        /// when all routes are registered. Multiple source-consuming mods share the native
        /// server; an already running server, including its chosen port, is left alone.
        /// Release calls are omitted, and builds against pre-4.3 APIs do nothing.
        /// </summary>
        [Conditional("DEBUG")]
        public static void Register(object owner, DependencyResolver resolver, string logTag)
        {
#if DEBUG && COI_UPDATE_4_3
            var events = resolver.Resolve<IGameLoopEvents>();
            events.RegisterRendererInitState(owner, () => events.InvokeInSyncNotSaved(() =>
            {
                try
                {
                    var server = resolver.Resolve<WebApiServer>();
                    if (server.IsRunning) return;

                    if (server.TryStart(WebApiServer.DEFAULT_PORT, out string error))
                        Log.Info($"{logTag} Debug build: web API at http://127.0.0.1:{server.Port}/; routes: http://127.0.0.1:{server.Port}/routes");
                    else
                        Log.Warning($"{logTag} Debug build: web API auto-start failed: {error}");
                }
                catch (Exception error)
                {
                    // Optional development tooling must not prevent a game from loading.
                    Log.Warning($"{logTag} Debug build: web API auto-start failed: {error.Message}");
                }
            }));
#endif
        }
    }
}
