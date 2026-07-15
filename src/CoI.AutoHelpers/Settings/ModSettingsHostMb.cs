using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mafi.Localization;
using Mafi.Unity;
using Mafi.Unity.Ui.Hud;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using UnityEngine;

namespace CoI.AutoHelpers.Settings
{
    internal sealed class ModSettingsHostMb : MonoBehaviour, IRootEscapeHandler
    {
        private const string HUD_ICON = "Assets/Base/Terrain/Surfaces/Decals/Alphabet/M.png";

        private readonly List<ModSettingsTab> m_tabs = new List<ModSettingsTab>();

        private HudController? m_hudController;
        private UiRoot? m_uiRoot;
        private IRootEscapeManager? m_escapeManager;
        private ModSettingsWindow? m_window;
        private bool m_initialized;
        private bool m_buttonAdded;
        private bool m_windowOpen;
        private bool m_escapeRegistered;
        private int m_openFrame;

        public void Initialize(
            HudController hudController,
            UiRoot uiRoot,
            IRootEscapeManager escapeManager)
        {
            if (m_hudController != hudController)
            {
                m_hudController = hudController;
                m_uiRoot = uiRoot;
                m_escapeManager = escapeManager;

                m_buttonAdded = false;
                m_windowOpen = false;
                m_escapeRegistered = false;
                m_initialized = false;
            }

            if (m_window == null)
            {
                m_window = new ModSettingsWindow(this);
                m_window.OnCloseStart += _ =>
                {
                    m_windowOpen = false;
                    ClearEscapeHandler();
                };
            }

            if (!m_initialized)
            {
                m_initialized = true;
                StartCoroutine(AddHudButtonDeferred());
            }
        }

        public void RegisterTab(ModSettingsTab tab)
        {
            if (tab == null)
                return;

            for (int i = m_tabs.Count - 1; i >= 0; i--)
            {
                ModSettingsTab existing = m_tabs[i];
                if (existing.ModId == tab.ModId && existing.Title.Value == tab.Title.Value)
                    m_tabs.RemoveAt(i);
            }

            m_tabs.Add(tab);
            m_tabs.Sort((a, b) =>
            {
                int byOrder = a.Order.CompareTo(b.Order);
                if (byOrder != 0)
                    return byOrder;
                return string.Compare(a.Title.Value, b.Title.Value, StringComparison.OrdinalIgnoreCase);
            });

            m_window?.RebuildTabs(m_tabs);
        }

        public void RegisterExternalTab(
            string modId,
            LocStrFormatted modName,
            LocStrFormatted title,
            int order,
            Func<UiComponent> buildContent,
            string? iconAssetPath,
            string? modIconAssetPath)
        {
            RegisterTab(new ModSettingsTab(modId, modName, title, order, buildContent, iconAssetPath, modIconAssetPath));
        }

        public void RegisterExternalTab(
            string modId,
            LocStrFormatted modName,
            LocStrFormatted title,
            int order,
            Func<UiComponent> buildContent,
            string? iconAssetPath)
        {
            RegisterTab(new ModSettingsTab(modId, modName, title, order, buildContent, iconAssetPath));
        }

        public void RegisterExternalTab(
            string modId,
            LocStrFormatted modName,
            LocStrFormatted title,
            int order,
            Func<UiComponent> buildContent)
        {
            RegisterTab(new ModSettingsTab(modId, modName, title, order, buildContent));
        }

        public void ToggleWindow()
        {
            if (m_window == null || m_uiRoot == null || m_escapeManager == null)
                return;

            if (m_windowOpen)
            {
                m_window.Close();
                m_windowOpen = false;
                ClearEscapeHandler();
                return;
            }

            m_window.RebuildTabs(m_tabs);
            m_window.Open(m_uiRoot);
            m_window.MakeMovable();
            m_windowOpen = true;
            m_openFrame = Time.frameCount;
            m_escapeManager.AddRootEscapeHandler(this);
            m_escapeRegistered = true;
        }

        public bool OnEscape()
        {
            if (!m_windowOpen || Time.frameCount <= m_openFrame)
                return false;

            m_window?.Close();
            m_windowOpen = false;
            ClearEscapeHandler();
            return true;
        }

        private void Update()
        {
            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                && Input.GetKeyDown(KeyCode.M))
            {
                ToggleWindow();
            }
        }

        private IEnumerator AddHudButtonDeferred()
        {
            yield return new WaitForSeconds(2.5f);
            AddHudButton();
            if (!m_buttonAdded)
            {
                yield return new WaitForSeconds(2.0f);
                AddHudButton();
            }
        }

        private void AddHudButton()
        {
            if (m_buttonAdded || m_hudController == null)
                return;

            try
            {
                FieldInfo? field = typeof(HudController).GetField(
                    "m_calendarControls",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                    return;

                UiComponent? calendar = field.GetValue(m_hudController) as UiComponent;
                if (calendar == null)
                    return;

                UiComponent? best = null;
                int bestCount = 0;
                FindNodeWithMostChildren(calendar, ref best, ref bestCount, 0);
                if (best == null || bestCount < 3)
                    return;

                ButtonIconGlow button = new ButtonIconGlow(HUD_ICON, ToggleWindow);
                button.Tooltip(ModSettings.Loc("OpenTooltip", "Open mod settings (Alt+M)"));

                best.InsertAt(0, button, false);
                m_buttonAdded = true;
            }
            catch (Exception ex)
            {
                Debug.Log("[AutoHelpers] Mod settings HUD button failed: " + ex);
            }
        }

        private static void FindNodeWithMostChildren(
            UiComponent node,
            ref UiComponent? best,
            ref int bestCount,
            int depth)
        {
            if (depth > 10)
                return;

            int count = node.ChildrenCount;
            if (count > bestCount)
            {
                best = node;
                bestCount = count;
            }

            foreach (UiComponent child in node.AllChildren)
                FindNodeWithMostChildren(child, ref best, ref bestCount, depth + 1);
        }

        private void ClearEscapeHandler()
        {
            if (!m_escapeRegistered || m_escapeManager == null)
                return;

            m_escapeManager.ClearRootEscapeHandler(this);
            m_escapeRegistered = false;
        }
    }
}
