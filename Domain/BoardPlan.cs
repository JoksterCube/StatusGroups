using System;
using System.Collections.Generic;

namespace JoksterCube.StatusGroups.Domain;

// A group as the planner sees it: its place and how it shows (never Default here).
internal readonly record struct PlannedGroup(string Id, int Order, GroupDisplay Display);

// One icon on the HUD: a single status, or a whole group stacked into one.
internal sealed class PlannedTile<T>(string groupId, bool stacked)
{
    internal string GroupId { get; } = groupId;
    internal bool Stacked { get; } = stacked;
    internal List<T> Members { get; } = [];
}

// A block of statuses. With Split By Group: one per group, with its name above it (Caption), and one without a caption
// for the stacked groups. Otherwise one block, no caption.
internal sealed class PlannedSection<T>(string? caption)
{
    internal string? Caption { get; } = caption;
    internal List<PlannedTile<T>> Tiles { get; } = [];
}

// Works out what goes where, from the statuses in the game's order. Knows nothing of Unity, so it can be tested.
internal static class BoardPlan
{
    internal static GroupDisplay Resolve(GroupDisplay display, StatusLayout layout)
    {
        if (display == GroupDisplay.Default) display = layout == StatusLayout.Stacked ? GroupDisplay.Stacked : GroupDisplay.Expanded;
        return display;
    }

    internal static List<PlannedSection<T>> Plan<T>(IReadOnlyList<T> effects, Func<T, PlannedGroup> groupOf, bool perGroup,
        bool sortByGroup, bool biggestFirst = false, Func<T, float?>? timeLeft = null, int perLine = int.MaxValue)
    {
        // The groups, in the order they first show up, each with its statuses in the game's order.
        List<PlannedGroup> groups = [];
        Dictionary<string, List<T>> members = [];
        foreach (T effect in effects)
        {
            PlannedGroup group = groupOf(effect);
            if (group.Display == GroupDisplay.Hidden) continue;
            if (!members.TryGetValue(group.Id, out List<T> list))
            {
                members[group.Id] = list = [];
                groups.Add(group);
            }
            list.Add(effect);
        }

        if (perGroup || sortByGroup)
        {
            // Stable: the same order every frame, also for groups with the same Order.
            groups.Sort((a, b) =>
            {
                // Longest First: the longest line first (a stacked group is one icon), then the one that stays that long longest,
                // then by Order.
                if (biggestFirst)
                {
                    int bySize = Width(Shown(b, members), perLine).CompareTo(Width(Shown(a, members), perLine));
                    if (bySize != 0) return bySize;
                    int byTime = Lasting(members[b.Id], timeLeft).CompareTo(Lasting(members[a.Id], timeLeft));
                    if (byTime != 0) return byTime;
                }
                int byOrder = a.Order.CompareTo(b.Order);
                return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
            });
        }

        List<PlannedSection<T>> sections = [];
        if (perGroup)
        {
            // Stacked groups share one block at the start; each expanded group gets its own.
            PlannedSection<T> stacks = new(null);
            foreach (PlannedGroup group in groups)
                if (group.Display == GroupDisplay.Stacked) stacks.Tiles.Add(Stack(group.Id, members[group.Id]));
            if (stacks.Tiles.Count > 0) sections.Add(stacks);
            foreach (PlannedGroup group in groups)
            {
                if (group.Display == GroupDisplay.Stacked) continue;
                PlannedSection<T> section = new(group.Id);
                AddSingles(section, group.Id, members[group.Id]);
                sections.Add(section);
            }
            // The stacks' block takes its place the same way, by its longest line, then by how long it stays that long.
            // Insertion sort: stable.
            if (biggestFirst)
                for (int i = 1; i < sections.Count; i++)
                {
                    PlannedSection<T> section = sections[i];
                    float lasting = Lasting(section, timeLeft);
                    int width = Width(section.Tiles.Count, perLine);
                    int j = i - 1;
                    for (; j >= 0 && (Width(sections[j].Tiles.Count, perLine) < width ||
                                      (Width(sections[j].Tiles.Count, perLine) == width && Lasting(sections[j], timeLeft) < lasting)); j--)
                        sections[j + 1] = sections[j];
                    sections[j + 1] = section;
                }
            return sections;
        }

        PlannedSection<T> all = new(null);
        if (sortByGroup)
        {
            foreach (PlannedGroup group in groups)
            {
                if (group.Display == GroupDisplay.Stacked) all.Tiles.Add(Stack(group.Id, members[group.Id]));
                else AddSingles(all, group.Id, members[group.Id]);
            }
        }
        else
        {
            // The game's order; a stacked group sits where its first status would.
            Dictionary<string, GroupDisplay> displays = [];
            foreach (PlannedGroup group in groups) displays[group.Id] = group.Display;
            HashSet<string> placed = [];
            foreach (T effect in effects)
            {
                PlannedGroup group = groupOf(effect);
                if (!displays.TryGetValue(group.Id, out GroupDisplay display)) continue;
                if (display != GroupDisplay.Stacked)
                {
                    PlannedTile<T> single = new(group.Id, false);
                    single.Members.Add(effect);
                    all.Tiles.Add(single);
                }
                else if (placed.Add(group.Id)) all.Tiles.Add(Stack(group.Id, members[group.Id]));
            }
        }
        if (all.Tiles.Count > 0) sections.Add(all);
        return sections;
    }

    // Shortest time left first, longest last, statuses without a time at the end: those from where you are (fire,
    // shelter) before those from worn gear, which sit last. Within each group's run when groups are kept together,
    // otherwise across the whole block. Stable, so equal times keep their place from frame to frame.
    // Run every frame, as times change; the order is nearly right already, so this is cheap.
    internal static void SortByTimeLeft<T>(List<PlannedSection<T>> sections, Func<T, float?> timeLeft, bool keepGroups,
        Func<T, bool>? worn = null)
    {
        foreach (PlannedSection<T> section in sections)
        {
            List<PlannedTile<T>> tiles = section.Tiles;
            for (int i = 1; i < tiles.Count; i++)
            {
                PlannedTile<T> tile = tiles[i];
                float left = TimeLeft(tile, timeLeft, worn);
                int j = i - 1;
                while (j >= 0 && (!keepGroups || tiles[j].GroupId == tile.GroupId) && TimeLeft(tiles[j], timeLeft, worn) > left)
                {
                    tiles[j + 1] = tiles[j];
                    j--;
                }
                tiles[j + 1] = tile;
            }
        }
    }

    // How many rows a block of this many statuses takes, at most perLine in each.
    internal static int LineCount(int count, int perLine) => count <= 0 ? 0 : (count - 1) / Math.Max(1, perLine) + 1;

    // How many statuses line `line` holds: the block shared out evenly, so 11 at 10 per line is 6 and 5, not 10 and 1.
    // The first lines take the extra ones.
    internal static int LineSize(int count, int lines, int line) => count / lines + (line < count % lines ? 1 : 0);

    // A time left in whole minutes ("27m", rounded up like the game's seconds) until secondsUnderMinutes are left, then
    // null: the game's own minutes-and-seconds text is used from there.
    internal static string? MinutesOnly(float secondsLeft, int secondsUnderMinutes)
    {
        if (secondsUnderMinutes <= 0 || secondsLeft <= secondsUnderMinutes * 60f) return null;
        return Math.Ceiling(secondsLeft / 60f) + "m";
    }

    // The room a time needs, as its shape: every digit made a 0 ("12:34" -> "00:00"), and never less than "00:00".
    // Measuring this rather than the time itself keeps lines and columns the same width while the time runs down.
    internal const string ShortestTimePattern = "00:00";

    internal static string TimePattern(string time)
    {
        char[] chars = time.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (char.IsDigit(chars[i])) chars[i] = '0';
        string pattern = new(chars);
        return pattern.Length < ShortestTimePattern.Length ? ShortestTimePattern : pattern;
    }

    // A stack counts as its member ending first. Without a time: MaxValue, or past it (infinity) for worn gear.
    private static float TimeLeft<T>(PlannedTile<T> tile, Func<T, float?> timeLeft, Func<T, bool>? worn)
    {
        float best = float.PositiveInfinity;
        foreach (T member in tile.Members)
        {
            float left = timeLeft(member) ?? (worn != null && worn(member) ? float.PositiveInfinity : float.MaxValue);
            if (left < best) best = left;
        }
        return best;
    }

    // How long a group keeps its size: until its first status runs out (a status without a time never does). The order
    // this gives only changes when a status comes or goes (every time runs down alike), and the plan is made again then.
    private static float Lasting<T>(List<T> statuses, Func<T, float?>? timeLeft)
    {
        if (timeLeft == null) return 0f;
        float first = float.MaxValue;
        foreach (T status in statuses) first = Math.Min(first, timeLeft(status) ?? float.MaxValue);
        return first;
    }

    // A stacked group is one icon until its last status runs out, so the stacks' block shrinks when its first stack ends.
    private static float Lasting<T>(PlannedSection<T> section, Func<T, float?>? timeLeft)
    {
        if (timeLeft == null) return 0f;
        float first = float.MaxValue;
        foreach (PlannedTile<T> tile in section.Tiles)
        {
            float last = float.MinValue;
            foreach (T status in tile.Members) last = Math.Max(last, timeLeft(status) ?? float.MaxValue);
            first = Math.Min(first, last);
        }
        return first;
    }

    // How long a block of this many icons is: its first (longest) line, once shared out over its lines.
    private static int Width(int count, int perLine) => count <= 0 ? 0 : LineSize(count, LineCount(count, perLine), 0);

    // How many icons a group shows.
    private static int Shown<T>(PlannedGroup group, Dictionary<string, List<T>> members) =>
        group.Display == GroupDisplay.Stacked ? 1 : members[group.Id].Count;

    private static PlannedTile<T> Stack<T>(string groupId, List<T> effects)
    {
        // A group of one is just that status.
        PlannedTile<T> tile = new(groupId, effects.Count > 1);
        tile.Members.AddRange(effects);
        return tile;
    }

    private static void AddSingles<T>(PlannedSection<T> section, string groupId, List<T> effects)
    {
        foreach (T effect in effects)
        {
            PlannedTile<T> tile = new(groupId, false);
            tile.Members.Add(effect);
            section.Tiles.Add(tile);
        }
    }
}
