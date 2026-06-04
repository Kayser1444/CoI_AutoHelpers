using System;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;

namespace CoI.AutoHelpers.Settings
{
    /// <summary>
    /// Describes one tab contributed by a mod to the shared AutoHelpers mod
    /// settings window.
    /// </summary>
    public sealed class ModSettingsTab
    {
        public string ModId { get; }

        public LocStrFormatted ModName { get; }

        public LocStrFormatted Title { get; }

        public int Order { get; }

        public string? IconAssetPath { get; }

        public Func<UiComponent> BuildContent { get; }

        public ModSettingsTab(
            string modId,
            LocStrFormatted modName,
            LocStrFormatted title,
            int order,
            Func<UiComponent> buildContent,
            string? iconAssetPath = null)
        {
            ModId = modId ?? string.Empty;
            ModName = modName;
            Title = title;
            Order = order;
            IconAssetPath = iconAssetPath;
            BuildContent = buildContent ?? throw new ArgumentNullException(nameof(buildContent));
        }
    }
}
