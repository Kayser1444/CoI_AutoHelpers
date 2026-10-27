using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core.Game;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;
using Mafi.Localization;

namespace CoI.AutoHelpers.VersionSelector;

// PRE_4_3_COMPAT: retire the selector and legacy payload when pre-4.3 support ends.
public sealed class SelectorEntry : IMod
{
    // The native loader reads static LocStr fields BEFORE constructing LoadedModData.
    // This explicit initializer installs the handoff before that constructor runs.
    // Test mode skips native localization scanning; the placeholder then fails closed.
    private static readonly LocStr s_loaderAnchor;
    static SelectorEntry()
    {
        var ctor = typeof(LoadedModData).GetConstructor(new[]
        {
            typeof(ModManifest), typeof(Option<Type>), typeof(ImmutableArray<Assembly>), typeof(Option<string>)
        }) ?? throw new MissingMethodException("LoadedModData constructor changed; version selection is unavailable.");
        new Harmony(typeof(SelectorEntry).Assembly.GetName().Name).Patch(ctor,
            prefix: new HarmonyMethod(typeof(SelectorEntry), nameof(SelectImplementation)));
        s_loaderAnchor = LocStr.Empty;
    }

    private static void SelectImplementation(ModManifest __0, ref Option<Type> __1, ref ImmutableArray<Assembly> __2)
    {
        if (!__1.HasValue || __1.Value != typeof(SelectorEntry)) return;
        var version = typeof(Log).Assembly.GetName().Version;
        string branch = version >= new Version(0, 8, 8, 0) ? "update-4.3" : "pre-update-4.3";
        string path = Path.Combine(__0.RootDirectoryPath, "implementations", branch, __0.Id + ".dll");
        if (!File.Exists(path)) throw new FileNotFoundException("Missing matching mod implementation: " + path, path);
        AssemblyName name = AssemblyName.GetAssemblyName(path);
        var payload = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().FullName == name.FullName);
        if (payload == null) payload = Assembly.Load(File.ReadAllBytes(path));
        if (payload.GetName().Name != __0.Id || payload.GetReferencedAssemblies()
            .Where(a => a.Name == "Mafi" || a.Name == "Mafi.Core")
            .Any(a => a.Version.Major != version.Major || a.Version.Minor != version.Minor || a.Version.Build != version.Build))
            throw new InvalidOperationException("Selected payload does not match the running game: " + path);
        var candidates = payload.GetExportedTypes().Where(t => typeof(IMod).IsAssignableFrom(t) && !t.IsAbstract).ToArray();
        if (candidates.Length != 1) throw new InvalidOperationException("Expected exactly one mod entry in " + path);
        LocalizationManager.ScanForStaticLocStrFields(payload);
        __1 = candidates[0];
        // Give the actual assembly to native config/dependency/serializer discovery.
        // Its original identity is retained, including all serialized KPIE types.
        __2 = __2.Where(a => a != typeof(SelectorEntry).Assembly).Concat(new[] { payload }).ToImmutableArray();
        Log.Info($"[{__0.Id}] Selected {branch} implementation for game assembly {version}: {path}");
    }

    public SelectorEntry(ModManifest manifest) => throw new InvalidOperationException(
        "Version selector handoff did not run. Native localization scanning and LoadedModData construction must be supported.");
    public ModManifest Manifest => throw new InvalidOperationException();
    public bool IsUiOnly => false;
    public Option<IConfig> ModConfig => Option<IConfig>.None;
    public ModJsonConfig JsonConfig => throw new InvalidOperationException();
    public void RegisterPrototypes(ProtoRegistrator registrator) => throw new InvalidOperationException();
    public void RegisterDependencies(DependencyResolverBuilder builder, ProtosDb db, bool loaded) => throw new InvalidOperationException();
    public void EarlyInit(DependencyResolver resolver) => throw new InvalidOperationException();
    public void Initialize(DependencyResolver resolver, bool loaded) => throw new InvalidOperationException();
    public void MigrateJsonConfig(VersionSlim version, Dict<string, object> values) => throw new InvalidOperationException();
    public void Dispose() { }
}
