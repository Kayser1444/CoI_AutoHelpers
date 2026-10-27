using CoI.AutoHelpers.Logging;
using Mafi;
using Mafi.Core.GameLoop;
#if COI_UPDATE_4_3
using Mafi.Core.WebApi;
#endif

internal static class Program
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static int Main()
    {
        var events = new FakeGameLoopEvents();
        var resolver = new DependencyResolver();
        resolver.Instances[typeof(IGameLoopEvents)] = events;
#if DEBUG && COI_UPDATE_4_3
        var server = new WebApiServer();
        resolver.Instances[typeof(WebApiServer)] = server;
        for (int mod = 0; mod < 4; mod++) ModDebugWebApi.Register(new object(), resolver, "[TEST]");
        Check(server.StartAttempts == 0, "Registration must not start the API before initialization.");
        events.RunRendererInit();
        Check(server.StartAttempts == 0, "Renderer initialization must defer API startup to sync.");
        events.RunSync();
        Check(server.StartAttempts == 1 && server.Port == 3073, "Four mods must start one server on port 3073.");
        Check(Log.Messages.Single().Contains("http://127.0.0.1:3073/routes"), "Log must include connection instructions.");
        Console.WriteLine("PASS: startup waits for renderer initialization and sync; four mods start one server.");

        var nextGame = new FakeGameLoopEvents();
        var nextServer = new WebApiServer();
        resolver.Instances[typeof(IGameLoopEvents)] = nextGame;
        resolver.Instances[typeof(WebApiServer)] = nextServer;
        ModDebugWebApi.Register(new object(), resolver, "[TEST]");
        nextGame.RunRendererInit();
        nextGame.RunSync();
        Check(nextServer.StartAttempts == 1, "A second game in the same process must also start its server.");
        Console.WriteLine("PASS: a second game starts its own server.");

        server.IsRunning = true;
        server.Port = 4567;
        resolver.Instances[typeof(IGameLoopEvents)] = events;
        resolver.Instances[typeof(WebApiServer)] = server;
        ModDebugWebApi.Register(new object(), resolver, "[TEST]");
        events.RunRendererInit();
        events.RunSync();
        Check(server.StartAttempts == 1 && server.Port == 4567, "An existing server's port must be preserved.");
        Console.WriteLine("PASS: an already running server is preserved.");

        server.IsRunning = false;
        server.FailStart = true;
        ModDebugWebApi.Register(new object(), resolver, "[TEST]");
        events.RunRendererInit();
        events.RunSync();
        Check(Log.Warnings.Last().Contains("port is busy"), "A bind failure must produce a warning.");
        server.ThrowOnStart = true;
        ModDebugWebApi.Register(new object(), resolver, "[TEST]");
        events.RunRendererInit();
        events.RunSync();
        Check(Log.Warnings.Last().Contains("test startup failure"), "An unexpected startup exception must be contained.");
        Console.WriteLine("PASS: startup failures warn without breaking initialization.");
#elif DEBUG
        ModDebugWebApi.Register(new object(), resolver, "[TEST]");
        Check(resolver.Resolutions == 0 && events.RendererInitCount == 0, "Pre-4.3 builds must not touch unavailable APIs.");
        Console.WriteLine("PASS: pre-4.3 build is a no-op without web API types.");
#else
        int ownerEvaluations = 0;
        object Owner() { ownerEvaluations++; return new object(); }
        ModDebugWebApi.Register(Owner(), resolver, "[TEST]");
        Check(ownerEvaluations == 0 && resolver.Resolutions == 0 && events.RendererInitCount == 0,
            "Release must omit the call and its arguments entirely.");
        Console.WriteLine("PASS: Release omits startup and argument evaluation.");
#endif
        return 0;
    }
}

namespace Mafi
{
    public sealed class DependencyResolver
    {
        public readonly Dictionary<Type, object> Instances = new();
        public int Resolutions;
        public T Resolve<T>() { Resolutions++; return (T)Instances[typeof(T)]; }
    }

    public static class Log
    {
        public static readonly List<string> Messages = new(), Warnings = new();
        public static void Info(string message) => Messages.Add(message);
        public static void Warning(string message) => Warnings.Add(message);
    }
}

namespace Mafi.Core.GameLoop
{
    public interface IGameLoopEvents
    {
        void RegisterRendererInitState(object owner, IEnumerator<string> callback);
        void InvokeInSyncNotSaved(Action callback);
    }

    public static class GameLoopEventsExtensions
    {
        public static void RegisterRendererInitState(this IGameLoopEvents events, object owner, Action callback)
            => events.RegisterRendererInitState(owner, Invoke(callback));
        private static IEnumerator<string> Invoke(Action callback) { callback(); yield break; }
    }

    internal sealed class FakeGameLoopEvents : IGameLoopEvents
    {
        private readonly Queue<IEnumerator<string>> m_rendererInit = new();
        private readonly Queue<Action> m_sync = new();
        public int RendererInitCount => m_rendererInit.Count;
        public void RegisterRendererInitState(object owner, IEnumerator<string> callback) => m_rendererInit.Enqueue(callback);
        public void InvokeInSyncNotSaved(Action callback) => m_sync.Enqueue(callback);
        public void RunRendererInit() { while (m_rendererInit.TryDequeue(out var callback)) while (callback.MoveNext()) { } }
        public void RunSync() { while (m_sync.TryDequeue(out var callback)) callback(); }
    }
}

#if COI_UPDATE_4_3
namespace Mafi.Core.WebApi
{
    public sealed class WebApiServer
    {
        public const int DEFAULT_PORT = 3073;
        public bool IsRunning;
        public int Port, StartAttempts;
        public bool FailStart, ThrowOnStart;
        public bool TryStart(int port, out string error)
        {
            StartAttempts++;
            if (ThrowOnStart) throw new InvalidOperationException("test startup failure");
            if (FailStart) { error = "port is busy"; return false; }
            Port = port;
            IsRunning = true;
            error = "";
            return true;
        }
    }
}
#endif
