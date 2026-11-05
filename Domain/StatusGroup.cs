using System;
using BepInEx.Configuration;
using UnityEngine;

namespace JoksterCube.StatusGroups.Domain;

// A group of statuses: built in, added by another mod (StatusGroupsApi) or named in the Custom Rules.
// Its id is also its settings' name ("Potions Display", "Potions Order") and what Custom Rules call it.
internal sealed class StatusGroup(string id, Func<string> name, int order, ConfigEntry<GroupDisplay> display,
    ConfigEntry<string> customName, ConfigEntry<int> orderEntry, ConfigEntry<string> icon)
{
    internal string Id { get; } = id;
    internal int DefaultOrder { get; } = order;
    internal Func<string> NameSource { get; set; } = name;
    internal ConfigEntry<GroupDisplay> Display { get; } = display;
    // The player's own name for the group; empty: the group's own (translated) name.
    internal ConfigEntry<string> CustomName { get; } = customName;
    internal ConfigEntry<int> Order { get; } = orderEntry;
    // The status whose icon marks the group (Show Group Icons), by name; empty: its painted icon or what its mod gave, else
    // the group's first status's.
    internal ConfigEntry<string> Icon { get; } = icon;
    // The icon another mod gave (StatusGroupsApi.SetGroupIcon).
    internal Func<Sprite?>? IconSource { get; set; }
    // The given icon is a pale silhouette the board tints off-white (the built-in groups', TempestRite's), not a full-color picture.
    internal bool IconTinted { get; set; }

    // The player's name for it, else in the game's language, for a mod that gives a $token or its own translated text.
    internal string Name
    {
        get
        {
            string custom = CustomName.Value?.Trim() ?? string.Empty;
            if (custom.Length > 0) return custom;
            string name;
            try { name = NameSource(); }
            catch (Exception) { name = Id; }
            if (string.IsNullOrEmpty(name)) return Id;
            return Localization.instance != null ? Localization.instance.Localize(name) : name;
        }
    }
}
