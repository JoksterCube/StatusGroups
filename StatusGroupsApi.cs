using System;
using JoksterCube.StatusGroups.Domain;
using UnityEngine;

namespace JoksterCube.StatusGroups;

// For other mods: put their statuses into groups of their own. Nothing to reference at build time: call it by
// reflection, so the mod still works without StatusGroups installed. Add
// [BepInDependency("JoksterCube.StatusGroups", BepInDependency.DependencyFlags.SoftDependency)] so StatusGroups loads
// first, then from Awake:
//
//   if (Chainloader.PluginInfos.TryGetValue("JoksterCube.StatusGroups", out PluginInfo info) && info.Instance)
//   {
//       Type api = info.Instance.GetType().Assembly.GetType("JoksterCube.StatusGroups.StatusGroupsApi");
//       api.GetMethod("AddGroup", [typeof(string), typeof(Func<string>), typeof(int)])
//           .Invoke(null, ["MyMod Buffs", (Func<string>)(() => "Buffs"), 2000]);
//       api.GetMethod("Assign", [typeof(string), typeof(string)]).Invoke(null, ["SE_MyMod_Haste", "MyMod Buffs"]);
//   }
//
// Group ids are also the names of the group's settings ("MyMod Buffs Display") and what players write in Custom Rules,
// so start them with the mod's name. Pick an order from the table in the README. Keep these signatures: mods built
// against older StatusGroups call them by name.
public static class StatusGroupsApi
{
    // Goes up when something is added here; nothing is ever taken away.
    public const int Version = 2;

    // A group. name: shown above its block (Split By Group) and on its stacked icon; looked up each time, so it can
    // follow the game's language (a $token works too). order: lower comes first. Adding a group again only changes its name.
    public static void AddGroup(string id, Func<string> name, int order) => Safe(() => GroupRegistry.Add(id, name, order));

    public static void AddGroup(string id, string name, int order) => AddGroup(id, () => name, order);

    // Version 2. The icon that marks a group beside its block (Show Group Icons), asked for each time it's drawn, so it
    // can come from an asset loaded later; null falls back to the group's first status's icon. A player's own
    // "<group> Icon" setting wins over it. Call after AddGroup. pale: a pale silhouette in the style of the built-in
    // groups' icons (and TempestRite's rites-page tabs), which StatusGroups tints off-white like them; otherwise drawn as it is.
    public static void SetGroupIcon(string groupId, Func<Sprite?> icon, bool pale) =>
        Safe(() =>
        {
            StatusGroup group = GroupRegistry.Get(groupId);
            group.IconSource = icon;
            group.IconTinted = pale;
            GroupRegistry.Touch();
        });

    public static void SetGroupIcon(string groupId, Func<Sprite?> icon) => SetGroupIcon(groupId, icon, false);

    // One status, by its internal name (the StatusEffect's object name, e.g. "SE_MyMod_Haste").
    public static void Assign(string statusName, string groupId) => Safe(() => StatusClassifier.Assign(statusName, groupId));

    // Every status whose internal name starts with this, unless assigned on its own.
    public static void AssignPrefix(string prefix, string groupId) => Safe(() => StatusClassifier.AssignPrefix(prefix, groupId));

    private static void Safe(Action action)
    {
        try { action(); }
        catch (Exception exception) { Plugin.ModLogger.LogWarning($"Another mod's StatusGroups call failed: {exception.Message}"); }
    }
}
