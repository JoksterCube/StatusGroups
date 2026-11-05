using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace JoksterCube.StatusGroups.Settings;

// Every setting is the player's own: the mod only changes how this game's HUD looks, so nothing is synced.
internal static class ConfigOptions
{
    private static ConfigFile? _configFile;
    // Configuration Manager lists higher Order first, so each group counts down in binding order.
    private static readonly Dictionary<string, int> NextOrder = [];

    internal static ConfigFile File => _configFile ?? throw new InvalidOperationException("ConfigOptions is not initialized.");

    internal static void Initialize(ConfigFile config) => _configFile = config;

    internal static ConfigEntry<T> Config<T>(string group, string name, T value, ConfigDescription description, bool advanced = false)
    {
        int order = NextOrder.TryGetValue(group, out int next) ? next : 1000;
        NextOrder[group] = order - 1;
        ConfigurationManagerAttributes attributes = new() { Order = order, IsAdvanced = advanced ? true : null };
        ConfigDescription extendedDescription = new(
            description.Description,
            description.AcceptableValues,
            (description.Tags ?? []).Append(attributes).ToArray());
        return File.Bind(group, name, value, extendedDescription);
    }

    internal static ConfigEntry<T> Config<T>(ConfigInfo<T> configInfo) =>
        Config(configInfo.Group, configInfo.Name, configInfo.DefaultValue, configInfo.Description, configInfo.IsAdvanced);
}
