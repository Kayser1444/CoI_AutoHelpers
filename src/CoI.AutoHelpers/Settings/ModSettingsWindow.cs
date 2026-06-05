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
            foreach (var group in tabs.GroupBy(tab => tab.ModId).OrderBy(g => g.Min(tab => tab.Order)))
            {
                List<ModSettingsTab> modTabEntries = group.OrderBy(tab => tab.Order).ThenBy(tab => tab.Title.Value).ToList();
                if (modTabEntries.Count == 0)
                    continue;

                ModSettingsTab first = modTabEntries[0];
                UiComponent modContent = modTabEntries.Count == 1
                    ? BuildTabContent(first)
                    : BuildNestedTabs(modTabEntries);

                modTabs.AddTab(first.ModName, modContent, first.ModIconAssetPath ?? first.IconAssetPath, null, false, true, null);
            }

            m_tabsSlot.Add(modTabs);
        }

        private static UiComponent BuildNestedTabs(IReadOnlyList<ModSettingsTab> tabs)
        {
            TabContainer nestedTabs = new TabContainer();
            foreach (ModSettingsTab tab in tabs)
                nestedTabs.AddTab(tab.Title, BuildTabContent(tab), tab.IconAssetPath, null, false, false, null);
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
