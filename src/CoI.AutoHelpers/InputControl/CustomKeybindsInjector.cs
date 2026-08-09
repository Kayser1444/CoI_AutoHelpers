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
