// PRE_4_3_COMPAT: legacy branches/API guards only; remove after Update 5 or when pre-4.3 support ends.
#if !COI_UPDATE_4_3
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using UnityEngine;

namespace CoI.AutoHelpers.InputControl;

public static class CustomKeybindsInjector
{
    private static KeyValuePair<PropertyInfo, KbAttribute>[] s_attributes = Array.Empty<KeyValuePair<PropertyInfo, KbAttribute>>();
    private static Dictionary<PropertyInfo, KeyBindings> s_defaults = new Dictionary<PropertyInfo, KeyBindings>();
    private static string s_modName = "";
    private static KbCategory s_customCategory;

    public static void ApplyPatches(Harmony harmony, string modName, Type keybindsType, bool persistInitialBindings = false)
    {
        s_modName = modName;
        // Allocate a stable pseudo-category so PlayerPrefs keys remain consistent across sessions.
        s_customCategory = (KbCategory)(100 + (StableHash(modName) % 1000));

        s_attributes = keybindsType.GetProperties(BindingFlags.Static | BindingFlags.Public)
            .Where(x => x.GetCustomAttributes(typeof(KbAttribute), false).Length > 0)
            .Select(x => new KeyValuePair<PropertyInfo, KbAttribute>(x, (KbAttribute)x.GetCustomAttributes(typeof(KbAttribute), false).First()))
            .ToArray();

        if (s_attributes.Length == 0) return;

        PatchVanillaKeyBinder(harmony);

        // Ensure all registered attributes use the custom category
        foreach (var kvp in s_attributes)
        {
            var field = typeof(KbAttribute).GetField("Category", BindingFlags.Instance | BindingFlags.Public);
            field?.SetValue(kvp.Value, s_customCategory);
        }

        // 1. Record defaults and load from PlayerPrefs
        foreach (var kvp in s_attributes)
        {
            var prop = kvp.Key;
            var attr = kvp.Value;
            var currentBinding = (KeyBindings)prop.GetValue(null);
            
            s_defaults[prop] = currentBinding;

            KeyBinding primary = currentBinding.Primary;
            if (UnityEngine.PlayerPrefs.HasKey(attr.PrefsIdPrimary))
            {
                string input = UnityEngine.PlayerPrefs.GetString(attr.PrefsIdPrimary, null);
                primary = primary.UpdateSelfFrom(input);
            }

            KeyBinding secondary = currentBinding.Secondary;
            if (UnityEngine.PlayerPrefs.HasKey(attr.PrefsIdSecondary))
            {
                string input = UnityEngine.PlayerPrefs.GetString(attr.PrefsIdSecondary, null);
                secondary = secondary.UpdateSelfFrom(input);
            }

            prop.SetValue(null, new KeyBindings(currentBinding.Mode, primary, secondary));
        }

        // A caller that supplied a legacy fallback requests a one-time migration into
        // the native controls store. Values restored from PlayerPrefs are retained.
        if (persistInitialBindings)
        {
            PersistCurrentBindings();
        }

        // 2. Patch UI getters
        harmony.Patch(
            original: AccessTools.Method(typeof(ShortcutsMap), nameof(ShortcutsMap.GetKeybindings)),
            postfix: new HarmonyMethod(typeof(CustomKeybindsInjector), nameof(GetKeybindings_Postfix))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(ShortcutsMap), nameof(ShortcutsMap.GetKeybindingsCount)),
            postfix: new HarmonyMethod(typeof(CustomKeybindsInjector), nameof(GetKeybindingsCount_Postfix))
        );

        // 3. Patch Save/Restore methods
        harmony.Patch(
            original: AccessTools.Method(typeof(ShortcutsStorage), nameof(ShortcutsStorage.ApplyChanges)),
            postfix: new HarmonyMethod(typeof(CustomKeybindsInjector), nameof(ApplyChanges_Postfix))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(ShortcutsStorage), nameof(ShortcutsStorage.RestoreDefaults)),
            postfix: new HarmonyMethod(typeof(CustomKeybindsInjector), nameof(RestoreDefaults_Postfix))
        );

        harmony.Patch(
            original: AccessTools.PropertyGetter(typeof(ShortcutsMap), nameof(ShortcutsMap.Categories)),
            postfix: new HarmonyMethod(typeof(CustomKeybindsInjector), nameof(GetCategories_Postfix))
        );
    }

    private static void GetCategories_Postfix(ref IEnumerable<KeyValuePair<KbCategory, Mafi.Localization.LocStrFormatted>> __result)
    {
        __result = __result.Concat(new[] { new KeyValuePair<KbCategory, Mafi.Localization.LocStrFormatted>(s_customCategory, new Mafi.Localization.LocStrFormatted(s_modName + " (Mod)")) });
    }

    /// <summary>
    /// Returns the logical keyboard state used while recording or evaluating a
    /// binding. On layouts with AltGr, Unity can expose one physical Right Alt
    /// press as RightAlt + AltGr + LeftControl. AltGr and its synthetic Ctrl
    /// must not become separate binding keys.
    /// </summary>
    public static bool IsLogicalKeyDown(KeyCode key)
    {
        if (key == KeyCode.AltGr)
            return false;

        if (key == KeyCode.LeftControl && UnityEngine.Input.GetKey(KeyCode.AltGr))
            return false;

        return UnityEngine.Input.GetKey(key);
    }

    public static bool IsLogicalKeyDownThisFrame(KeyCode key)
    {
        if (key == KeyCode.AltGr)
            return false;

        if (key == KeyCode.LeftControl && UnityEngine.Input.GetKey(KeyCode.AltGr))
            return false;

        return UnityEngine.Input.GetKeyDown(key);
    }

    /// <summary>
    /// Recreates the registered key-binding attributes after a mod has injected
    /// its translation bundle. KbAttribute snapshots its title and tooltip in
    /// the constructor, while custom keybinds are discovered before late mod
    /// localization normally runs.
    /// </summary>
    public static int RefreshRegisteredLocalizations()
    {
        if (s_attributes.Length == 0)
            return 0;

        var refreshed = new KeyValuePair<PropertyInfo, KbAttribute>[s_attributes.Length];
        for (int i = 0; i < s_attributes.Length; i++)
        {
            KeyValuePair<PropertyInfo, KbAttribute> current = s_attributes[i];
            KbAttribute attribute = current.Value;
            string? tooltip = attribute.Tooltip.HasValue
                ? attribute.Tooltip.Value.TranslatedString
                : null;
            string? requiredDlc = attribute.RequiredDlc.IsNone
                ? null
                : attribute.RequiredDlc.Value;

            KbAttribute rebound = new KbAttribute(
                attribute.Category,
                attribute.GroupId,
                attribute.Title.Value,
                tooltip,
                attribute.IgnoreConflicts,
                attribute.IsPrimaryReadonly,
                requiredDlc);
            refreshed[i] = new KeyValuePair<PropertyInfo, KbAttribute>(current.Key, rebound);
        }

        s_attributes = refreshed;
        return refreshed.Length;
    }

    private static void PatchVanillaKeyBinder(Harmony harmony)
    {
        Type? controlsTabType = AccessTools.TypeByName("Mafi.Unity.InputControl.GameMenu.Settings.ControlsTab");
        Type? keyBinderType = controlsTabType?.GetNestedType("KeyBinder", BindingFlags.NonPublic);
        MethodInfo? inputUpdate = keyBinderType == null ? null : AccessTools.Method(keyBinderType, "InputUpdate");
        if (inputUpdate == null)
            return;

        harmony.Patch(
            inputUpdate,
            transpiler: new HarmonyMethod(typeof(CustomKeybindsInjector), nameof(ControlsTabKeyBinderInputUpdate_Transpiler)));
    }

    private static IEnumerable<CodeInstruction> ControlsTabKeyBinderInputUpdate_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo? getKey = AccessTools.Method(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKey), new[] { typeof(KeyCode) });
        MethodInfo? logicalGetKey = AccessTools.Method(typeof(CustomKeybindsInjector), nameof(IsLogicalKeyDown));
        if (getKey == null || logicalGetKey == null)
        {
            foreach (CodeInstruction instruction in instructions)
                yield return instruction;
            yield break;
        }

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(getKey))
                instruction.operand = logicalGetKey;
            yield return instruction;
        }
    }

    private static void GetKeybindings_Postfix(KbCategory category, IMain main, ref IEnumerable<KeyValuePair<PropertyInfo, KbAttribute>> __result)
    {
        var ours = s_attributes.Where(x => x.Value.Category == category);
        __result = __result.Concat(ours);
    }

    private static void GetKeybindingsCount_Postfix(IMain main, ref int __result)
    {
        __result += s_attributes.Length;
    }

    private static void ApplyChanges_Postfix()
    {
        foreach (var kvp in s_attributes)
        {
            var prop = kvp.Key;
            var attr = kvp.Value;
            var currentBinding = (KeyBindings)prop.GetValue(null);
            var defaultBinding = s_defaults[prop];

            if (currentBinding.Primary.AreSame(defaultBinding.Primary))
            {
                if (UnityEngine.PlayerPrefs.GetString(attr.PrefsIdPrimary, null) != null)
                    UnityEngine.PlayerPrefs.DeleteKey(attr.PrefsIdPrimary);
            }
            else
            {
                UnityEngine.PlayerPrefs.SetString(attr.PrefsIdPrimary, currentBinding.Primary.ToString());
            }

            if (currentBinding.Secondary.AreSame(defaultBinding.Secondary))
            {
                if (UnityEngine.PlayerPrefs.GetString(attr.PrefsIdSecondary, null) != null)
                    UnityEngine.PlayerPrefs.DeleteKey(attr.PrefsIdSecondary);
            }
            else
            {
                UnityEngine.PlayerPrefs.SetString(attr.PrefsIdSecondary, currentBinding.Secondary.ToString());
            }
        }
        UnityEngine.PlayerPrefs.Save();
    }

    private static void PersistCurrentBindings()
    {
        foreach (var kvp in s_attributes)
        {
            var binding = (KeyBindings)kvp.Key.GetValue(null);
            var attr = kvp.Value;
            UnityEngine.PlayerPrefs.SetString(attr.PrefsIdPrimary, binding.Primary.ToString());
            UnityEngine.PlayerPrefs.SetString(attr.PrefsIdSecondary, binding.Secondary.ToString());
        }

        UnityEngine.PlayerPrefs.Save();
    }

    private static void RestoreDefaults_Postfix()
    {
        foreach (var kvp in s_attributes)
        {
            var prop = kvp.Key;
            prop.SetValue(null, s_defaults[prop]);
        }
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            const int offset = (int)2166136261;
            const int prime = 16777619;
            int hash = offset;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= prime;
            }
            return hash & int.MaxValue;
        }
    }
}

#else
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Core.Mods;
using Mafi.Unity.InputControl;
using UnityEngine;
using System.Reflection.Emit;

namespace CoI.AutoHelpers.InputControl;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ModKeybindAttribute : Attribute
{
    public readonly string Id, Title, Tooltip;
    public readonly bool IgnoreConflicts;
    public ModKeybindAttribute(string id, string title, string tooltip, bool ignoreConflicts = false)
    {
        Id = id; Title = title; Tooltip = tooltip; IgnoreConflicts = ignoreConflicts;
    }
}

/// <summary>Registers native mod shortcuts in 4.3 while keeping the legacy property-facing contract.</summary>
public static class CustomKeybindsInjector
{
    private static Type? s_bindingsType;
    private static readonly Dictionary<PropertyInfo, KeyBindings> s_defaults = new();
    private static readonly Dictionary<PropertyInfo, KbHandle> s_handles = new();
    private static ShortcutsManager? s_manager;

    public static void ApplyPatches(Harmony harmony, string modName, Type keybindsType, bool persistInitialBindings = false)
    {
        s_bindingsType = keybindsType;
        foreach (var property in keybindsType.GetProperties(BindingFlags.Static | BindingFlags.Public))
            if (property.GetCustomAttribute<ModKeybindAttribute>() != null)
            {
                s_defaults[property] = (KeyBindings)property.GetValue(null);
                s_handles[property] = BuildHandle(modName, property, s_defaults[property]);
            }
        harmony.Patch(AccessTools.Method(typeof(ShortcutsStorage), nameof(ShortcutsStorage.ApplyChanges)),
            postfix: new HarmonyMethod(typeof(CustomKeybindsInjector), nameof(SyncBindings)));
        PatchControlsLayout(harmony);
    }

    private static void PatchControlsLayout(Harmony harmony)
    {
        Type? controls = AccessTools.TypeByName("Mafi.Unity.InputControl.GameMenu.Settings.ControlsTab");
        MethodInfo? buildOwnerBlock = controls == null ? null : AccessTools.Method(controls, "buildOwnerBlock");
        if (buildOwnerBlock == null)
        {
            Log.Warning("[AutoHelpers] Controls layout method was not found; shortcut registration is unaffected.");
            return;
        }

        // Each mod compiles its own copy of this helper. Apply the shared native
        // UI correction once, even when several mods register shortcuts.
        if (Harmony.GetPatchInfo(buildOwnerBlock)?.Transpilers.Any(p =>
            p.PatchMethod.DeclaringType?.FullName == typeof(CustomKeybindsInjector).FullName &&
            p.PatchMethod.Name == nameof(ControlsLayout_Transpiler)) == true) return;

        try
        {
            harmony.Patch(buildOwnerBlock,
                transpiler: new HarmonyMethod(typeof(CustomKeybindsInjector), nameof(ControlsLayout_Transpiler)));
        }
        catch (Exception error)
        {
            Log.Warning($"[AutoHelpers] Controls layout correction could not be applied: {error.Message}");
        }
    }

    private static IEnumerable<CodeInstruction> ControlsLayout_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var divisions = Enumerable.Range(1, Math.Max(0, code.Count - 2)).Where(i =>
            code[i - 1].opcode == OpCodes.Ldc_I4_2 && code[i].opcode == OpCodes.Div &&
            code[i + 1].opcode.Name.StartsWith("stloc", StringComparison.Ordinal)).ToArray();
        if (divisions.Length != 1)
        {
            Log.Warning("[AutoHelpers] Controls column threshold was not recognized; keeping the native layout.");
            return code;
        }

        // Native count / 2 is zero for one shortcut, so it creates an empty left
        // column before placing the first section. Require at least one row
        // before splitting, preserving native balancing for counts of 2 or more.
        code.InsertRange(divisions[0] + 1, new[]
        {
            new CodeInstruction(OpCodes.Ldc_I4_1),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Math), nameof(Math.Max), new[] { typeof(int), typeof(int) }))
        });
        return code;
    }

    // Must run once per game, before the registration session closes.
    public static void Register(DependencyResolver resolver, IMod owner)
    {
        if (s_bindingsType == null) return;
        s_manager = resolver.Resolve<ShortcutsManager>();
        IShortcutsRegistry registry = resolver.Resolve<IShortcutsRegistry>();
        foreach (var handle in s_handles.Values) registry.Add(owner, handle);
        SyncBindings();
    }

    private static KbHandle BuildHandle(string modId, PropertyInfo property, KeyBindings keys)
    {
        ModKeybindAttribute attr = property.GetCustomAttribute<ModKeybindAttribute>()!;
        var title = Mafi.Localization.LocalizationManager.GetLocalizedString0Arg("Kb_" + attr.Id + "__label", attr.Title, "Mod shortcut label", ignoreDuplicates: true);
        var tooltip = Mafi.Localization.LocalizationManager.GetLocalizedString0Arg("Kb_" + attr.Id + "__tooltip", attr.Tooltip, "Mod shortcut tooltip", ignoreDuplicates: true);
        KbHandleBuilder builder = KbHandleBuilder.Start(modId + "." + attr.Id, title)
            .SetPrimary(keys.Primary).SetSecondary(keys.Secondary).SetTooltip(tooltip)
            .SetSupersededIds(attr.Id);
        if (attr.IgnoreConflicts) builder.SetFlags(KbFlags.IgnoreConflicts);
        return builder.Build();
    }

    public static KbHandle GetHandle(string propertyName) =>
        s_handles.First(p => p.Key.Name == propertyName).Value;

    public static void SetBindings(string propertyName, KeyBindings keys)
    {
        var entry = s_handles.FirstOrDefault(p => p.Key.Name == propertyName);
        if (entry.Value == null || !entry.Value.IsRegistered) return;
        var changes = new Mafi.Collections.Lyst<KeyValuePair<KbHandle, KeyBindings>>(1);
        changes.Add(new KeyValuePair<KbHandle, KeyBindings>(entry.Value, keys));
        ShortcutsStorage.ApplyChanges(changes);
    }

    private static void SyncBindings()
    {
        foreach (var entry in s_handles)
            entry.Key.SetValue(null, entry.Value.Current);
    }

    public static bool IsPressed(KeyBindings keys) => s_manager != null
        && s_handles.Values.Any(h => h.Current == keys && s_manager.IsDown(h));
    public static bool IsHeld(KeyBindings keys) => s_manager != null
        && s_handles.Values.Any(h => h.Current == keys && s_manager.IsOn(h));
    public static KeyBindings FromPrimaryKeys(params KeyCode[] keys)
    {
        if (!KbChord.TryParsePrefsString(string.Join(" + ", keys.Select(k => k.ToString())), out KbChord chord))
            throw new ArgumentException("A shortcut must contain modifiers and at most one trigger key.", nameof(keys));
        return new KeyBindings(chord, KbChord.Empty);
    }
    public static int RefreshRegisteredLocalizations() => s_handles.Count;

    public static bool IsLogicalKeyDown(KeyCode key)
    {
        if (key == KeyCode.AltGr)
            return false;

        if (key == KeyCode.LeftControl && UnityEngine.Input.GetKey(KeyCode.AltGr))
            return false;

        return UnityEngine.Input.GetKey(key);
    }

    public static bool IsLogicalKeyDownThisFrame(KeyCode key)
    {
        if (key == KeyCode.AltGr)
            return false;

        if (key == KeyCode.LeftControl && UnityEngine.Input.GetKey(KeyCode.AltGr))
            return false;

        return UnityEngine.Input.GetKeyDown(key);
    }

}
#endif
