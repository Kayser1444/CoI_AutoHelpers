using System;
using System.Collections.Generic;
using System.Reflection;
using Mafi.Localization;
using Mafi.Unity;
using Mafi.Unity.Ui.Hud;
using Mafi.Unity.UiToolkit;
using UnityEngine;

namespace CoI.AutoHelpers.Settings
{
    /// <summary>
    /// Shared entry point for registering mod settings tabs.
    /// </summary>
    public static class ModSettings
    {
        private const string HOST_OBJECT_NAME = "CoI.AutoHelpers.ModSettingsHost";
        private const string HOST_TYPE_NAME = "CoI.AutoHelpers.Settings.ModSettingsHostMb";

        private static readonly List<ModSettingsTab> s_pendingTabs = new List<ModSettingsTab>();

        private static ModSettingsHostMb? s_localHost;

        public static void EnsureInitialized(
            HudController hudController,
            UiRoot uiRoot,
            IRootEscapeManager escapeManager)
        {
            if (TryFindExternalHost(out object? externalHost))
            {
                InvokeInitialize(externalHost!, hudController, uiRoot, escapeManager);
                FlushPendingTabs();
                return;
            }

            if (s_localHost == null)
            {
                GameObject hostObject = GameObject.Find(HOST_OBJECT_NAME);
                if (hostObject == null)
                {
                    hostObject = new GameObject(HOST_OBJECT_NAME);
                    UnityEngine.Object.DontDestroyOnLoad(hostObject);
                }

                s_localHost = hostObject.GetComponent<ModSettingsHostMb>();
                if (s_localHost == null)
                    s_localHost = hostObject.AddComponent<ModSettingsHostMb>();
            }

            s_localHost.Initialize(hudController, uiRoot, escapeManager);
            FlushPendingTabs();
        }

        public static void RegisterTab(ModSettingsTab tab)
        {
            if (tab == null)
                return;

            if (TryFindExternalHost(out object? externalHost))
            {
                InvokeRegisterTab(externalHost!, tab);
                return;
            }

            if (s_localHost != null)
            {
                s_localHost.RegisterTab(tab);
                return;
            }

            s_pendingTabs.Add(tab);
        }

        internal static LocStrFormatted Loc(string idSuffix, string text)
        {
            LocStr loc = LocalizationManager.CreateAlreadyLocalizedStr(
                "CoI_AutoHelpers_ModSettings_" + idSuffix,
                text);
            return loc.AsFormatted;
        }

        private static void FlushPendingTabs()
        {
            if (s_pendingTabs.Count == 0)
                return;

            ModSettingsTab[] tabs = s_pendingTabs.ToArray();
            s_pendingTabs.Clear();
            foreach (ModSettingsTab tab in tabs)
                RegisterTab(tab);
        }

        private static bool TryFindExternalHost(out object? host)
        {
            host = null;
            GameObject hostObject = GameObject.Find(HOST_OBJECT_NAME);
            if (hostObject == null)
                return false;

            foreach (MonoBehaviour component in hostObject.GetComponents<MonoBehaviour>())
            {
                if (component == null)
                    continue;

                Type type = component.GetType();
                if (type.FullName != HOST_TYPE_NAME)
                    continue;

                if (type.Assembly == typeof(ModSettings).Assembly)
                    continue;

                host = component;
                return true;
            }

            return false;
        }

        private static void InvokeInitialize(
            object host,
            HudController hudController,
            UiRoot uiRoot,
            IRootEscapeManager escapeManager)
        {
            MethodInfo? method = host.GetType().GetMethod(
                "Initialize",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            method?.Invoke(host, new object[] { hudController, uiRoot, escapeManager });
        }

        private static void InvokeRegisterTab(object host, ModSettingsTab tab)
        {
            MethodInfo? method = host.GetType().GetMethod(
                "RegisterExternalTab",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method != null)
            {
                method.Invoke(host, new object[]
                {
                    tab.ModId,
                    tab.ModName,
                    tab.Title,
                    tab.Order,
                    tab.BuildContent
                });
            }
        }
    }
}
