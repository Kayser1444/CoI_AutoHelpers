using System.Text.Json;
using System.Text.RegularExpressions;
using CoI.AutoHelpers.Compatibility;
using Mono.Cecil;

internal static class Program
{
    private static int s_checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        s_checks++;
    }

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && (args[0] == "--references" || args[0] == "--switching-references" || args[0] == "--switching-release"))
            {
                InstalledReferenceContracts(args[1], args[2], args[0] != "--references", args[0] == "--switching-release");
                return 0;
            }
            AudioContracts();
            if (args.Length != 4)
                throw new ArgumentException("Arguments: game Managed directory, Mods directory, original findings JSON, output JSON.");
            MetadataContracts(args[0], args[1], args[2], args[3]);
            Console.WriteLine($"PASS: {s_checks} compatibility contracts.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void InstalledReferenceContracts(string managed, string mods, bool switching = false, bool release = false)
    {
        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managed);
        resolver.AddSearchDirectory(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory());
        string framework = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319");
        if (Directory.Exists(framework)) resolver.AddSearchDirectory(framework);
        var parameters = new ReaderParameters { AssemblyResolver = resolver };
        foreach (string project in new[] { "AutoTerrainDesignations", "AutoForestryDesignations", "DesignerToolkit", "KaysersPreIndustrialEra" })
        {
            string root = Path.Combine(mods, project);
            using var game = AssemblyDefinition.ReadAssembly(Path.Combine(managed, "Mafi.dll"));
            string branch = game.Name.Version >= new Version(0, 8, 8, 0) ? "update-4.3" : "pre-update-4.3";
            string path = switching ? Path.Combine(root, "implementations", branch, project + ".dll")
                : Path.Combine(root, project + ".dll");
            using var mod = AssemblyDefinition.ReadAssembly(path, parameters);
            Check(AllTypes(mod.MainModule.Types).All(t => t.FullName != "CoI.AutoHelpers.Logging.ModDebugWebApi"),
                project + " must leave web API startup to the game launch command.");
            if (release)
            {
                CheckRelease(mod);
                using var selector = AssemblyDefinition.ReadAssembly(Path.Combine(root, project + ".Loader.dll"), parameters);
                CheckRelease(selector);
            }
            var failures = new List<string>();
            foreach (var reference in mod.MainModule.AssemblyReferences.Where(r => r.Name.StartsWith("Mafi", StringComparison.Ordinal)))
            {
                var actual = resolver.Resolve(reference);
                if (actual.Name.Version != reference.Version)
                    failures.Add($"{reference.Name} compiled for {reference.Version}, installed {actual.Name.Version}");
            }
            foreach (var reference in mod.MainModule.GetTypeReferences().Where(r => r.Namespace.StartsWith("Mafi", StringComparison.Ordinal)))
            {
                try { if (reference.Resolve() == null) failures.Add("missing type " + reference.FullName); }
                catch (Exception error) when (error is AssemblyResolutionException or ResolutionException)
                { failures.Add("missing type " + reference.FullName); }
            }
            foreach (var reference in mod.MainModule.GetMemberReferences().Where(r => r.DeclaringType.Namespace.StartsWith("Mafi", StringComparison.Ordinal)))
            {
                try
                {
                    bool resolved = reference switch
                    {
                        MethodReference method => method.Resolve() != null,
                        FieldReference field => field.Resolve() != null,
                        _ => true
                    };
                    if (!resolved) failures.Add("missing member " + reference.FullName);
                }
                catch (Exception error) when (error is AssemblyResolutionException or ResolutionException)
                { failures.Add("missing member " + reference.FullName); }
            }
            Check(failures.Count == 0, project + ": " + string.Join("; ", failures.Take(16)));
            Console.WriteLine("PASS: " + project + " installed game assembly versions, types and members resolve.");
        }
    }

    private static void CheckRelease(AssemblyDefinition assembly)
    {
        var debug = assembly.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "System.Diagnostics.DebuggableAttribute");
        Check(debug == null || debug.ConstructorArguments.Count == 0 ||
            (Convert.ToInt32(debug.ConstructorArguments[0].Value) & 256) == 0,
            assembly.Name.Name + " was compiled with Debug optimizations disabled.");
    }

    private static void AudioContracts()
    {
        var modern = new ModernOwner();
        UiAudio.Bind(modern, "InvalidOp")();
        Check(modern.UiSounds.Last == Mafi.Unity.Audio.UiSound.InvalidOp, "4.3 enum sound is played");
        UiAudio.Bind(modern, "AssignStructure")();
        Check(modern.UiSounds.Last == Mafi.Unity.Audio.UiSound.AssignStructure, "4.3 route sound is played");
        var legacy = new LegacyOwner();
        UiAudio.Bind(legacy, "ButtonClick")();
        Check(legacy.AudioDb.Path == "Assets/Unity/UserInterface/Audio/ButtonClick.prefab", "legacy prefab path retained");
        Check(legacy.AudioDb.Source.Plays == 1, "legacy source is played once");
        var fieldOwner = new LegacyFieldOwner();
        UiAudio.Bind(fieldOwner, "InvalidOp")();
        Check(fieldOwner.InvalidOpSound.Plays == 1, "legacy UiRoot field fallback works");
        UiAudio.Bind(new object(), "InvalidOp")();
        Check(true, "missing optional audio remains harmless");
    }

    private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> roots)
    {
        foreach (TypeDefinition type in roots)
        {
            yield return type;
            foreach (TypeDefinition nested in AllTypes(type.NestedTypes)) yield return nested;
        }
    }

    private static void MetadataContracts(string managed, string mods, string input, string output)
    {
        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managed);
        resolver.AddSearchDirectory(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory());
        string framework = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319");
        if (Directory.Exists(framework)) resolver.AddSearchDirectory(framework);
        var parameters = new ReaderParameters { AssemblyResolver = resolver };
        var game = new[] { "Mafi", "Mafi.Core", "Mafi.Base", "Mafi.Unity" }
            .Select(n => AssemblyDefinition.ReadAssembly(Path.Combine(managed, n + ".dll"), parameters)).ToArray();
        var types = game.SelectMany(a => AllTypes(a.MainModule.Types)).ToArray();
        TypeDefinition Type(string fullName) => types.Single(t => t.FullName == fullName);
        bool Field(string fullName, string member) => Type(fullName).Fields.Any(f => f.Name == member);
        Check(!Field("Mafi.Unity.UiToolkit.UiRoot", "InvalidOpSound"), "removed invalid-operation sound confirmed");
        Check(!Field("Mafi.Unity.InputControl.BuildingsAssigner", "m_assignClickAudio"), "removed assignment audio confirmed");
        Check(!Field("Mafi.Unity.InputControl.BuildingsAssigner", "m_unassignClickAudio"), "removed unassignment audio confirmed");
        Check(!Field("Mafi.Unity.Ui.Controllers.TransportBuildController", "m_noTurnBtn"), "removed toolbar anchor confirmed");
        Check(Field("Mafi.Unity.Ui.Controllers.TransportBuildController", "m_onlyStraight"), "new hold-state anchor exists");
        Check(Field("Mafi.Core.Prototypes.Proto", "<IsObsolete>k__BackingField"), "KPIE warning is a false positive");

        foreach (string project in new[] { "AutoTerrainDesignations", "AutoForestryDesignations", "DesignerToolkit", "KaysersPreIndustrialEra" })
        {
            string path = Path.Combine(mods, project, "bin", "Debug", "net48", project + ".dll");
            using var assembly = AssemblyDefinition.ReadAssembly(path, parameters);
            var failures = new List<string>();
            foreach (MemberReference reference in assembly.MainModule.GetMemberReferences())
            {
                if (!reference.DeclaringType.Namespace.StartsWith("Mafi", StringComparison.Ordinal)) continue;
                try
                {
                    bool resolved = reference switch
                    {
                        MethodReference method => method.Resolve() != null,
                        FieldReference field => field.Resolve() != null,
                        _ => true
                    };
                    if (!resolved) failures.Add(reference.FullName);
                }
                catch (AssemblyResolutionException) { failures.Add(reference.FullName); }
                catch (ResolutionException) { failures.Add(reference.FullName); }
            }
            Check(failures.Count == 0, project + " has unresolved game API references: " + string.Join("; ", failures));
            Console.WriteLine(project + ": compiled game member references resolve.");
        }

        var findings = JsonSerializer.Deserialize<List<Finding>>(File.ReadAllText(input))!;
        var results = new List<object>();
        var totals = new Dictionary<string, int>();
        foreach (Finding finding in findings)
        {
            Match lookup = Regex.Match(finding.title, @"typeof\((.+?)\)\.(.+)$");
            string verdict = "dynamic/manual";
            string resolvedType = "";
            if (lookup.Success)
            {
                string name = lookup.Groups[1].Value;
                string member = lookup.Groups[2].Value;
                TypeDefinition[] candidates = types.Where(t => t.FullName.Replace('/', '.') == name
                    || t.FullName.Replace('/', '.').EndsWith("." + name, StringComparison.Ordinal)).ToArray();
                string sourcePath = Path.Combine(mods, finding.project, finding.location.Split(':')[0]);
                if (candidates.Length > 1 && File.Exists(sourcePath))
                {
                    string source = File.ReadAllText(sourcePath);
                    string[] imports = Regex.Matches(source, @"(?m)^using ([\w.]+);")
                        .Select(m => m.Groups[1].Value).ToArray();
                    var imported = candidates.Where(t => imports.Any(ns => t.FullName.Replace('/', '.') == ns + "." + name)).ToArray();
                    if (imported.Length == 1) candidates = imported;
                }
                if (candidates.Length == 1 && member is not ("GetField" or "GetMethod" or "GetProperty" or "Method" or "Field" or "Constructor"))
                {
                    resolvedType = candidates[0].FullName;
                    verdict = HasMember(candidates[0], member) ? "member-exists" : "missing-member";
                }
            }
            else if (finding.title.StartsWith("Reflection type lookup:", StringComparison.Ordinal))
            {
                Match typeLookup = Regex.Match(finding.title, @"TypeByName\('([^']+)'\)");
                if (typeLookup.Success)
                {
                    resolvedType = typeLookup.Groups[1].Value;
                    verdict = types.Any(t => t.FullName == resolvedType) ? "type-exists" : "missing-type";
                }
            }
            else if (finding.title.StartsWith("Harmony target:", StringComparison.Ordinal))
            {
                Match harmony = Regex.Match(finding.title, @"Harmony target: (\w+)\.(\w+)$");
                TypeDefinition[] candidates = types.Where(t => t.Name == harmony.Groups[1].Value).ToArray();
                if (harmony.Success && candidates.Length == 1)
                {
                    resolvedType = candidates[0].FullName;
                    verdict = HasMember(candidates[0], harmony.Groups[2].Value) ? "member-exists" : "missing-member";
                }
            }
            totals[verdict] = totals.GetValueOrDefault(verdict) + 1;
            results.Add(new { finding.status, finding.project, finding.location, finding.title, verdict, resolvedType });
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new { totals, results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Original report metadata audit: " + JsonSerializer.Serialize(totals));
        foreach (var assembly in game) assembly.Dispose();
    }

    private static bool HasMember(TypeDefinition type, string member)
    {
        for (TypeDefinition? current = type; current != null; current = current.BaseType?.Resolve())
            if (current.Fields.Any(f => f.Name == member) || current.Properties.Any(p => p.Name == member)
                || current.Methods.Any(m => m.Name == member)) return true;
        return false;
    }

    private sealed record Finding(string status, string title, string project, string location, string evidence);
    private sealed class ModernOwner { public readonly Player UiSounds = new(); }
    private sealed class Player
    {
        public Mafi.Unity.Audio.UiSound Last;
        public void Play(Mafi.Unity.Audio.UiSound sound) => Last = sound;
    }
    private sealed class LegacyOwner { public readonly AudioDatabase AudioDb = new(); }
    private sealed class AudioDatabase
    {
        public string? Path;
        public readonly Source Source = new();
        public Source GetSharedAudioUi(string path) { Path = path; return Source; }
    }
    private sealed class LegacyFieldOwner { public readonly Source InvalidOpSound = new(); }
    private sealed class Source { public int Plays; public void Play() => Plays++; }
}

namespace Mafi.Unity.Audio
{
    internal enum UiSound { InvalidOp, ButtonClick, AssignStructure }
}
