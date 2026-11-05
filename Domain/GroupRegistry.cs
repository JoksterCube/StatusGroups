using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using JoksterCube.StatusGroups.Settings;
using static JoksterCube.StatusGroups.Settings.Constants.Groups;

namespace JoksterCube.StatusGroups.Domain;

// Every group, by id (ignoring case), each with its own Display and Order settings, bound when the group first appears.
internal static class GroupRegistry
{
    // The built-in groups, with their places. Other mods pick theirs from the table in the README, or put their
    // statuses into these (TempestRite's blessings are Buffs, its kinship waits Cooldowns).
    internal const string Buffs = "Buffs";
    internal const string Cooldowns = "Cooldowns";
    internal const string Negatives = "Negatives";
    internal const string Passive = "Passive";

    // Groups named only in the Custom Rules.
    internal const int RuleGroupOrder = 5000;

    // Places for other JoksterCube mods' groups, in among the built-in ones; they win over the order the mod asks for.
    private static readonly Dictionary<string, int> KnownOrders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TempestRite Domain"] = 200,
        ["TempestRite World"] = 500
    };

    private static readonly Dictionary<string, StatusGroup> ById = new(StringComparer.OrdinalIgnoreCase);

    // Changes whenever a group or a group's setting does, so cached plans are made again.
    internal static int Version { get; private set; }

    internal static void AddBuiltIn()
    {
        Add(Buffs, () => Buffs, 100);
        Add(Cooldowns, () => Cooldowns, 800);
        Add(Negatives, () => Negatives, 1000);
        Add(Passive, () => Passive, 400);
        // Their own painted icons (GroupIcons).
        foreach (string id in new[] { Buffs, Cooldowns, Negatives, Passive })
        {
            StatusGroup group = ById[id];
            group.IconSource = () => GroupIcons.Get(id);
            group.IconTinted = true;
        }
    }

    // A group added again (another mod's, after a reload) keeps its settings and takes the new name.
    internal static StatusGroup Add(string id, Func<string> name, int order, GroupDisplay display = GroupDisplay.Default)
    {
        id = CleanId(id);
        if (ById.TryGetValue(id, out StatusGroup existing))
        {
            existing.NameSource = name;
            Version++;
            return existing;
        }
        if (KnownOrders.TryGetValue(id, out int known)) order = known;
        ConfigEntry<GroupDisplay> displayEntry = ConfigOptions.Config(GroupSettings.Group, $"{id} Display", display, GroupSettings.Display(id));
        ConfigEntry<string> nameEntry = ConfigOptions.Config(GroupSettings.Group, $"{id} Name", string.Empty, GroupSettings.Name(id));
        ConfigEntry<int> orderEntry = ConfigOptions.Config(GroupSettings.Group, $"{id} Order", order, GroupSettings.Order(id));
        ConfigEntry<string> iconEntry = ConfigOptions.Config(GroupSettings.Group, $"{id} Icon", string.Empty, GroupSettings.Icon(id));
        displayEntry.SettingChanged += (_, _) => Version++;
        nameEntry.SettingChanged += (_, _) => Version++;
        orderEntry.SettingChanged += (_, _) => Version++;
        iconEntry.SettingChanged += (_, _) => Version++;
        StatusGroup group = new(id, name, order, displayEntry, nameEntry, orderEntry, iconEntry);
        ById[id] = group;
        Version++;
        return group;
    }

    // The group, made on the spot when a Custom Rule names one that isn't there yet.
    internal static StatusGroup Get(string id)
    {
        id = CleanId(id);
        if (ById.TryGetValue(id, out StatusGroup group)) return group;
        string name = id;
        return Add(id, () => name, RuleGroupOrder);
    }

    internal static bool Exists(string id) => ById.ContainsKey(CleanId(id));

    internal static void Touch() => Version++;

    // Setting names may not hold = [ ] " ' \ or line breaks.
    internal static string CleanId(string id)
    {
        char[] chars = id.Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (chars[i] is '=' or '[' or ']' or '"' or '\'' or '\\' or '\n' or '\r' or '\t') chars[i] = '_';
        string clean = new string(chars).Trim();
        return clean.Length == 0 ? Passive : clean;
    }
}
