using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Executes the game's real ModsLoader with a tiny IMod fixture, so it can test
// the handoff without loading Unity's native-only UI types in the full mods.
string managed = Path.GetFullPath(args[0]), root = Path.GetFullPath(args[1]);
AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
{
    foreach (string dir in new[] { managed, root })
    {
        string path = Path.Combine(dir, new AssemblyName(request.Name).Name + ".dll");
        if (File.Exists(path)) return Assembly.LoadFrom(path);
    }
    return null;
};
Directory.CreateDirectory(root);
using (var source = AssemblyDefinition.ReadAssembly(args[2]))
{
    // A loader-shaped IMod with a harmless constructor is a minimal payload.
    source.Name.Name = "SelectorFixture";
    var type = source.MainModule.Types.Single(t => t.Name == "SelectorEntry");
    type.Name = "FixtureMod";
    type.Methods.Remove(type.Methods.Single(m => m.Name == ".cctor"));
    var ctor = type.Methods.Single(m => m.IsConstructor);
    ctor.Body.Instructions.Clear();
    var il = ctor.Body.GetILProcessor();
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Call, source.MainModule.ImportReference(typeof(object).GetConstructor(Type.EmptyTypes)));
    il.Emit(OpCodes.Ret);
    foreach (string branch in new[] { "pre-update-4.3", "update-4.3" })
    {
        foreach (var reference in source.MainModule.AssemblyReferences.Where(a => a.Name == "Mafi" || a.Name == "Mafi.Core"))
            reference.Version = branch == "update-4.3" ? new Version(0,8,8,0) : new Version(0,8,7,0);
        source.CustomAttributes.Clear();
        var marker = new CustomAttribute(source.MainModule.ImportReference(typeof(AssemblyMetadataAttribute).GetConstructor(new[] { typeof(string), typeof(string) })));
        marker.ConstructorArguments.Add(new CustomAttributeArgument(source.MainModule.TypeSystem.String, "FixtureBranch"));
        marker.ConstructorArguments.Add(new CustomAttributeArgument(source.MainModule.TypeSystem.String, branch));
        source.CustomAttributes.Add(marker);
        string dir = Path.Combine(root, "implementations", branch);
        Directory.CreateDirectory(dir);
        source.Write(Path.Combine(dir, "SelectorFixture.dll"));
    }
}
File.Copy(args[2], Path.Combine(root, "SelectorFixture.Loader.dll"), true);
File.Copy(typeof(HarmonyLib.Harmony).Assembly.Location, Path.Combine(root, "0Harmony.dll"), true);
var mafi = Assembly.LoadFrom(Path.Combine(managed, "Mafi.dll"));
var core = Assembly.LoadFrom(Path.Combine(managed, "Mafi.Core.dll"));
var localization = mafi.GetType("Mafi.Localization.LocalizationManager", true);
Type manifestType = core.GetType("Mafi.Core.Mods.ModManifest", true);
object manifest = manifestType.GetMethod("CreateEmpty").Invoke(null, new object[] { "SelectorFixture" });
manifestType.GetField("RootDirectoryPath").SetValue(manifest, root);
var immutableType = manifestType.GetField("PrimaryDlls").FieldType;
// Use Mafi's own immutable array storage via its public constructor.
var array = Activator.CreateInstance(immutableType, new object[] { new[] { "0Harmony.dll", "SelectorFixture.Loader.dll" } });
manifestType.GetField("PrimaryDlls").SetValue(manifest, array);
manifestType.GetField("NonLockingDllLoad").SetValue(manifest, true);
Type availableType = core.GetType("Mafi.Core.Mods.AvailableModData", true);
object available = Activator.CreateInstance(availableType, manifest);
var loader = core.GetType("Mafi.Core.Mods.ModsLoader", true);
object loaded = loader.GetMethod("GetOrLoadMod").Invoke(null, new[] { available });
Console.WriteLine(loaded);
var loadedType = loaded.GetType();
object modTypeOption = loadedType.GetField("ModType").GetValue(loaded);
var payloadType = (Type)(modTypeOption.GetType().GetProperty("Value")?.GetValue(modTypeOption)
    ?? modTypeOption.GetType().GetField("Value").GetValue(modTypeOption));
if (payloadType.Name != "FixtureMod") throw new Exception("Native loader did not select the implementation: " + loaded);
string expected = mafi.GetName().Version >= new Version(0, 8, 8, 0) ? "update-4.3" : "pre-update-4.3";
if (payloadType.Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single().Value != expected)
    throw new Exception("Selected the wrong game branch.");
var assemblies = loadedType.GetField("Assemblies").GetValue(loaded);
var sequence = (IEnumerable<Assembly>)assemblies.GetType().GetMethod("AsEnumerable").Invoke(assemblies, null);
var names = sequence.Select(a => a.GetName().Name).ToArray();
if (!names.Contains("SelectorFixture") || names.Any(n => n.EndsWith(".Loader")))
    throw new Exception("Native assembly discovery list is incorrect.");
object cached = loader.GetMethod("GetOrLoadMod").Invoke(null, new[] { available });
if (!ReferenceEquals(cached, loaded)) throw new Exception("Native loaded-mod caching changed.");
Console.WriteLine("PASS: native loader selects the payload and preserves native assembly discovery/cache for " + mafi.GetName().Version);
