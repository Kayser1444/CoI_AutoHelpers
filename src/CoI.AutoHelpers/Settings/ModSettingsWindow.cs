using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;

namespace CoI.AutoHelpers.Settings
{
    internal sealed class ModSettingsWindow : Window
    {
        private readonly Column m_tabsSlot;
        private string? m_activeModId;
        private readonly Dictionary<string, int> m_activeNestedTabOrderByModId =
            new Dictionary<string, int>();

        public ModSettingsWindow(ModSettingsHostMb host)
            : base(ModSettings.Loc("Title", "Mod Settings"), false)
        {
            WindowSize(new Px(760f), new Px(720f));
            CloseOnClickOutside();

            m_tabsSlot = new Column().FlexGrow(1f).AlignItemsStretch();
            Body.Add(m_tabsSlot);
        }

        public void RebuildTabs(IReadOnlyList<ModSettingsTab> tabs)
        {
            m_tabsSlot.Clear();

            if (tabs.Count == 0)
            {
                m_tabsSlot.Add(new Label(ModSettings.Loc("NoTabs", "No mod settings registered.")));
                return;
            }

            TabContainer modTabs = new TabContainer();
            var modIdByContent = new Dictionary<UiComponent, string>();
            modTabs.OnTabActivate(() =>
            {
                UiComponent? activeTab = modTabs.ActiveTab.ValueOrNull;
                if (activeTab != null && modIdByContent.TryGetValue(activeTab, out string modId))
                    m_activeModId = modId;
            });

            string? requestedActiveModId = m_activeModId;
            bool selectedTabExists = !string.IsNullOrWhiteSpace(requestedActiveModId)
                && tabs.Any(tab => tab.ModId == requestedActiveModId);
            foreach (var group in tabs.GroupBy(tab => tab.ModId).OrderBy(g => g.Min(tab => tab.Order)))
            {
                List<ModSettingsTab> modTabEntries = group.OrderBy(tab => tab.Order).ThenBy(tab => tab.Title.Value).ToList();
                if (modTabEntries.Count == 0)
                    continue;

                ModSettingsTab first = modTabEntries[0];
                UiComponent modContent = modTabEntries.Count == 1
                    ? BuildTabContent(first)
                    : BuildNestedTabs(modTabEntries);

                modIdByContent[modContent] = first.ModId;
                modTabs.AddTab(
                    first.ModName,
                    modContent,
                    first.ModIconAssetPath ?? first.IconAssetPath,
                    null,
                    selectedTabExists && first.ModId == requestedActiveModId,
                    true);
            }

            m_tabsSlot.Add(modTabs);
        }

        private UiComponent BuildNestedTabs(IReadOnlyList<ModSettingsTab> tabs)
        {
            TabContainer nestedTabs = new TabContainer();
            var orderByContent = new Dictionary<UiComponent, int>();
            nestedTabs.OnTabActivate(() =>
            {
                UiComponent? activeTab = nestedTabs.ActiveTab.ValueOrNull;
                if (activeTab != null && orderByContent.TryGetValue(activeTab, out int order))
                    m_activeNestedTabOrderByModId[tabs[0].ModId] = order;
            });

            bool hasRememberedTab = m_activeNestedTabOrderByModId.TryGetValue(
                tabs[0].ModId, out int rememberedOrder)
                && tabs.Any(tab => tab.Order == rememberedOrder);
            foreach (ModSettingsTab tab in tabs)
            {
                UiComponent content = BuildTabContent(tab);
                orderByContent[content] = tab.Order;
                nestedTabs.AddTab(
                    tab.Title,
                    content,
                    tab.IconAssetPath,
                    null,
                    hasRememberedTab && tab.Order == rememberedOrder,
                    false);
            }
            return nestedTabs;
        }

        private static UiComponent BuildTabContent(ModSettingsTab tab)
        {
            try
            {
                return tab.BuildContent();
            }
            catch
            {
                return new Label(ModSettings.Loc("TabError", "This settings tab failed to load."));
            }
        }
    }
}
