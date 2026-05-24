using Mafi.Core.Mods;

namespace CoI.AutoHelpers.Persistence
{
    /// <summary>
    /// Central factory for mod-owned JSON persistence stores. Change
    /// <see cref="CreateDefault"/> when the project-wide default storage
    /// location changes.
    /// </summary>
    public static class ModStateJsonStores
    {
        public static IModStateJsonStore CreateDefault(ModJsonConfig jsonConfig, string stateKey)
        {
            return CreateVanillaModConfig(jsonConfig, stateKey);
        }

        public static IModStateJsonStore CreateVanillaModConfig(ModJsonConfig jsonConfig, string stateKey)
        {
            return new VanillaModJsonConfigStateStore(jsonConfig, stateKey);
        }
    }
}
