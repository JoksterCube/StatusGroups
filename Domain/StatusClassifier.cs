using System;
using System.Collections.Generic;
using JoksterCube.StatusGroups.Common;
using JoksterCube.StatusGroups.Settings;

namespace JoksterCube.StatusGroups.Domain;

// Which group a status belongs to. First match wins:
// 1. the player's Custom Rules;
// 2. what another mod said (StatusGroupsApi), by the status's internal name, then by name prefix;
// 3. built in, into four groups:
//    Cooldowns: a crossed-out icon (the healing meads' wait and the like);
//    Negatives: bad weather and anything harmful;
//    Buffs: Rested, the guardian power, meads and anything else drunk or eaten that isn't food;
//    Passive: where you stand (shelter, resting, fire, cozy, warm), worn gear and set bonuses;
// 4. anything else: Buffs when it runs out, Passive when it lasts while something holds.
// Each status's group is remembered by its name, until a rule, a mod or the item list changes.
internal static class StatusClassifier
{
    private static readonly Dictionary<int, StatusGroup> Cache = [];
    private static int _cacheVersion = -1;
    private static int _rulesVersion;
    private static StatusRules _rules = new();

    private static readonly Dictionary<string, string> Assigned = new(StringComparer.Ordinal);
    private static readonly List<(string Prefix, string Group)> Prefixes = [];

    // Statuses given by items, from the item list (made again when it changes).
    private static ObjectDB? _db;
    private static int _dbItems = -1;
    private static readonly HashSet<int> Consumed = [];
    private static readonly HashSet<int> Worn = [];

    // Rested is timed and is what's watched away from home, so it's a buff; the rest only last while you stay put.
    private static readonly int Rested = "Rested".GetStableHashCode();
    private static readonly HashSet<int> PassiveNames = Hashes("Resting", "Shelter", "CampFire", "Cozy", "Warm");
    private static readonly HashSet<int> NegativeNames = Hashes(
        "Wet", "Cold", "Freezing",
        "Burning", "Frost", "Lightning", "Poison", "Smoked", "Spirit", "Tared", "Encumbered", "Slimed", "Harpooned", "Puke");

    // JoksterCube's mods that don't place their statuses themselves.
    private static readonly (string Prefix, string Group)[] KnownMods =
    [
        ("PingMark_", GroupRegistry.Buffs),
        ("SE_ProtectSpawnerFarms_", GroupRegistry.Passive)
    ];

    private static readonly HashSet<int> Logged = [];

    internal static void SetRules(string text)
    {
        _rules = StatusRules.Parse(text);
        _rulesVersion++;
    }

    internal static void Assign(string statusName, string groupId)
    {
        Assigned[statusName] = groupId;
        _rulesVersion++;
    }

    internal static void AssignPrefix(string prefix, string groupId)
    {
        Prefixes.RemoveAll(p => p.Prefix == prefix);
        Prefixes.Add((prefix, groupId));
        // Longest first, so a narrower prefix wins.
        Prefixes.Sort((a, b) => b.Prefix.Length.CompareTo(a.Prefix.Length));
        _rulesVersion++;
    }

    // A number that changes whenever a status could land in another group.
    internal static int Version => _rulesVersion * 397 + GroupRegistry.Version * 31 + _dbItems;

    internal static StatusGroup GroupOf(StatusEffect effect, Player player)
    {
        RefreshItems();
        int version = Version;
        if (version != _cacheVersion)
        {
            Cache.Clear();
            _cacheVersion = version;
        }
        int hash = effect.NameHash();
        if (Cache.TryGetValue(hash, out StatusGroup group)) return group;

        group = GroupRegistry.Get(Find(effect, player));
        // Adding a rule's group bumps the version: remember it against the new one.
        _cacheVersion = Version;
        Cache[hash] = group;
        if (PluginConfig.LogStatuses.IsOn() && Logged.Add(hash))
            Plugin.ModLogger.LogInfo($"Status '{effect.name}' ({Shown(effect)}) is in group '{group.Id}'.");
        return group;
    }

    private static string Find(StatusEffect effect, Player player)
    {
        string name = effect.name;
        int hash = effect.NameHash();

        string? ruled = _rules.GroupOf(name, Shown(effect));
        if (ruled != null) return ruled;
        if (Assigned.TryGetValue(name, out string assigned)) return assigned;
        foreach ((string prefix, string group) in Prefixes)
            if (name.StartsWith(prefix, StringComparison.Ordinal)) return group;

        if (effect.m_cooldownIcon) return GroupRegistry.Cooldowns;
        if (NegativeNames.Contains(hash) || effect is SE_Wet or SE_Burning or SE_Poison or SE_Frost or SE_Smoke or SE_Harpooned or SE_Puke)
            return GroupRegistry.Negatives;
        if (hash == Rested || effect is SE_Rested) return GroupRegistry.Buffs;
        if (PassiveNames.Contains(hash) || effect is SE_Cozy || Worn.Contains(hash)) return GroupRegistry.Passive;
        if (name.StartsWith("GP_", StringComparison.Ordinal) || (player.m_guardianSE && player.m_guardianSE.NameHash() == hash)
            || Consumed.Contains(hash) || effect.m_category.Length > 0)
            return GroupRegistry.Buffs;
        foreach ((string prefix, string group) in KnownMods)
            if (name.StartsWith(prefix, StringComparison.Ordinal)) return group;
        return effect.m_ttl > 0f ? GroupRegistry.Buffs : GroupRegistry.Passive;
    }

    // The name shown in game, in the game's language.
    internal static string Shown(StatusEffect effect) =>
        string.IsNullOrEmpty(effect.m_name) ? string.Empty
            : Localization.instance != null ? Localization.instance.Localize(effect.m_name) : effect.m_name;

    // The statuses items give: drunk or eaten without being food (meads and the like), and worn (equipment, sets).
    private static void RefreshItems()
    {
        ObjectDB db = ObjectDB.instance;
        if (!db) return;
        if (db == _db && db.m_items.Count == _dbItems) return;
        _db = db;
        _dbItems = db.m_items.Count;
        Consumed.Clear();
        Worn.Clear();
        foreach (UnityEngine.GameObject prefab in db.m_items)
        {
            if (!prefab || prefab.GetComponent<ItemDrop>() is not { } drop) continue;
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            if (shared.m_consumeStatusEffect && shared.m_food <= 0f) Consumed.Add(shared.m_consumeStatusEffect.NameHash());
            if (shared.m_equipStatusEffect) Worn.Add(shared.m_equipStatusEffect.NameHash());
            if (shared.m_setStatusEffect) Worn.Add(shared.m_setStatusEffect.NameHash());
        }
    }

    private static HashSet<int> Hashes(params string[] names)
    {
        HashSet<int> hashes = [];
        foreach (string name in names) hashes.Add(name.GetStableHashCode());
        return hashes;
    }
}
