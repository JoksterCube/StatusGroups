using System.Collections.Generic;
using System.Linq;
using JoksterCube.StatusGroups.Domain;
using Xunit;

namespace StatusGroups.Tests;

public class RuleTests
{
    [Fact]
    public void Matches_internal_or_shown_name_ignoring_case()
    {
        StatusRules rules = StatusRules.Parse("rested=Weather, SE_MyMod_*=My Mod");
        Assert.Equal("Weather", rules.GroupOf("Rested", "Rested"));
        Assert.Equal("My Mod", rules.GroupOf("SE_MyMod_Haste", "Haste"));
        Assert.Null(rules.GroupOf("Wet", "Wet"));
    }

    [Fact]
    public void Matches_by_name_shown_in_game()
    {
        StatusRules rules = StatusRules.Parse("Crypt-Delver*=Loot");
        Assert.Equal("Loot", rules.GroupOf("SE_TempestRite_CryptDelver", "Crypt-Delver Blessing"));
    }

    [Fact]
    public void First_rule_wins_and_broken_parts_are_skipped()
    {
        StatusRules rules = StatusRules.Parse("=Nothing, junk, Wet=, Wet=First; Wet=Second\nCold=Third");
        Assert.Equal(3, rules.Count);
        Assert.Equal("First", rules.GroupOf("Wet", ""));
        Assert.Equal("Third", rules.GroupOf("Cold", ""));
    }

    [Fact]
    public void Special_characters_are_literal()
    {
        StatusRules rules = StatusRules.Parse("GP_(Eikthyr).x=Power");
        Assert.Equal("Power", rules.GroupOf("GP_(Eikthyr).x", ""));
        Assert.Null(rules.GroupOf("GP_(Eikthyr)Ax", ""));
    }

    [Fact]
    public void Empty_rules_match_nothing() => Assert.Null(StatusRules.Parse(null).GroupOf("Wet", "Wet"));
}

public class PlanTests
{
    // Statuses as "group:name", groups ordered by the table below.
    private static readonly Dictionary<string, int> Orders = new() { ["Potions"] = 200, ["Cooldowns"] = 300, ["Other"] = 9000 };

    private static List<PlannedSection<string>> Plan(string[] effects, StatusLayout layout, bool split, bool sort = true,
        Dictionary<string, GroupDisplay>? displays = null) =>
        BoardPlan.Plan(effects, effect =>
        {
            string group = effect.Split(':')[0];
            GroupDisplay display = displays != null && displays.TryGetValue(group, out GroupDisplay d) ? d : GroupDisplay.Default;
            return new PlannedGroup(group, Orders[group], BoardPlan.Resolve(display, layout));
        }, split, sort);

    private static string Describe(PlannedTile<string> tile) =>
        tile.Stacked ? $"[{string.Join(" ", tile.Members)}]" : tile.Members.Single();

    private static readonly string[] Mixed = ["Other:Wet", "Potions:Health", "Cooldowns:Kin", "Potions:Stamina"];

    private static List<PlannedSection<string>> PlanBiggest(string[] effects, bool split,
        Dictionary<string, GroupDisplay>? displays = null) =>
        BoardPlan.Plan(effects, effect =>
        {
            string group = effect.Split(':')[0];
            GroupDisplay display = displays != null && displays.TryGetValue(group, out GroupDisplay d) ? d : GroupDisplay.Default;
            return new PlannedGroup(group, Orders[group], BoardPlan.Resolve(display, StatusLayout.Icons));
        }, split, sortByGroup: true, biggestFirst: true,
            timeLeft: effect => effect.Split(':') is { Length: 3 } parts ? float.Parse(parts[2]) : null);

    [Fact]
    public void Longest_row_wins_not_most_statuses()
    {
        // At 3 per line: Potions' 4 are 2 and 2, so Other's 3 in one row is longer. Cooldowns' 2 is shortest.
        string[] effects = ["Potions:A", "Potions:B", "Potions:C", "Potions:D", "Other:A", "Other:B", "Other:C", "Cooldowns:A", "Cooldowns:B"];
        List<PlannedSection<string>> plan = BoardPlan.Plan(effects, effect =>
        {
            string group = effect.Split(':')[0];
            return new PlannedGroup(group, Orders[group], GroupDisplay.Expanded);
        }, perGroup: true, sortByGroup: true, biggestFirst: true, timeLeft: _ => null, perLine: 3);
        Assert.Equal(["Other", "Potions", "Cooldowns"], plan.Select(s => s.Caption));
    }

    [Fact]
    public void Ties_go_to_the_group_whose_first_status_runs_out_latest()
    {
        // All show two. Potions' first runs out at 100, Cooldowns' at 50 (though its other lasts 600), Other's at 200.
        string[] effects = ["Potions:Health:300", "Potions:Stamina:100", "Cooldowns:A:50", "Cooldowns:B:600", "Other:Wet", "Other:Cold:200"];
        Assert.Equal(["Other", "Potions", "Cooldowns"], PlanBiggest(effects, split: true).Select(s => s.Caption));
    }

    private static readonly string[] Sizes =
        ["Potions:Health", "Cooldowns:A", "Cooldowns:B", "Cooldowns:C", "Other:Wet", "Other:Cold"];

    [Fact]
    public void Biggest_group_first_ties_by_order()
    {
        List<PlannedSection<string>> plan = PlanBiggest(Sizes, split: true);
        Assert.Equal(["Cooldowns", "Other", "Potions"], plan.Select(s => s.Caption));
    }

    [Fact]
    public void Biggest_first_in_one_block_and_stacks_count_as_one()
    {
        List<PlannedSection<string>> plan = PlanBiggest(Sizes, split: false,
            new() { ["Cooldowns"] = GroupDisplay.Stacked });
        Assert.Equal(["Other:Wet", "Other:Cold", "Potions:Health", "[Cooldowns:A Cooldowns:B Cooldowns:C]"],
            plan[0].Tiles.Select(Describe));
    }

    [Fact]
    public void Stacks_block_takes_its_place_by_size()
    {
        List<PlannedSection<string>> plan = PlanBiggest(Sizes, split: true, new() { ["Cooldowns"] = GroupDisplay.Stacked });
        Assert.Equal(["Other", null, "Potions"], plan.Select(s => s.Caption));
    }

    [Fact]
    public void One_block_sorts_by_group_order()
    {
        List<PlannedSection<string>> plan = Plan(Mixed, StatusLayout.Icons, split: false);
        Assert.Single(plan);
        Assert.Null(plan[0].Caption);
        Assert.Equal(["Potions:Health", "Potions:Stamina", "Cooldowns:Kin", "Other:Wet"], plan[0].Tiles.Select(Describe));
    }

    [Fact]
    public void One_block_unsorted_keeps_game_order()
    {
        List<PlannedSection<string>> plan = Plan(Mixed, StatusLayout.Icons, split: false, sort: false);
        Assert.Equal(Mixed, plan[0].Tiles.Select(Describe));
    }

    [Fact]
    public void Split_gives_each_group_its_block()
    {
        List<PlannedSection<string>> plan = Plan(Mixed, StatusLayout.Icons, split: true, sort: false);
        Assert.Equal(["Potions", "Cooldowns", "Other"], plan.Select(s => s.Caption));
        Assert.Equal(2, plan[0].Tiles.Count);
    }

    [Fact]
    public void Stacked_makes_one_tile_per_group_and_a_lone_status_stays_itself()
    {
        List<PlannedSection<string>> plan = Plan(Mixed, StatusLayout.Stacked, split: false);
        Assert.Equal(["[Potions:Health Potions:Stamina]", "Cooldowns:Kin", "Other:Wet"], plan[0].Tiles.Select(Describe));
        Assert.True(plan[0].Tiles[0].Stacked);
        Assert.False(plan[0].Tiles[1].Stacked);
    }

    [Fact]
    public void Hidden_groups_are_left_out()
    {
        List<PlannedSection<string>> plan = Plan(Mixed, StatusLayout.Icons, split: true,
            displays: new() { ["Potions"] = GroupDisplay.Hidden });
        Assert.Equal(["Cooldowns", "Other"], plan.Select(s => s.Caption));
    }

    [Fact]
    public void Split_puts_stacked_groups_in_one_block_on_top()
    {
        List<PlannedSection<string>> plan = Plan(Mixed, StatusLayout.Icons, split: true,
            displays: new() { ["Potions"] = GroupDisplay.Stacked, ["Other"] = GroupDisplay.Stacked });
        Assert.Null(plan[0].Caption);
        Assert.Equal(["[Potions:Health Potions:Stamina]", "Other:Wet"], plan[0].Tiles.Select(Describe));
        Assert.Equal(["Cooldowns"], plan.Skip(1).Select(s => s.Caption));
    }

    [Fact]
    public void Unsorted_stack_sits_where_its_first_status_was()
    {
        List<PlannedSection<string>> plan = Plan(Mixed, StatusLayout.Icons, split: false, sort: false,
            displays: new() { ["Potions"] = GroupDisplay.Stacked });
        Assert.Equal(["Other:Wet", "[Potions:Health Potions:Stamina]", "Cooldowns:Kin"], plan[0].Tiles.Select(Describe));
    }

    [Fact]
    public void Stacked_split_keeps_every_stack_in_one_block()
    {
        List<PlannedSection<string>> plan = Plan(Mixed, StatusLayout.Stacked, split: true);
        Assert.Single(plan);
        Assert.Null(plan[0].Caption);
        Assert.Equal(["[Potions:Health Potions:Stamina]", "Cooldowns:Kin", "Other:Wet"], plan[0].Tiles.Select(Describe));
    }

    [Theory]
    [InlineData("13:09", "00:00")]
    [InlineData("8:51", "00:00")]
    [InlineData("52", "00:00")]
    [InlineData("5", "00:00")]
    [InlineData("120:00", "000:00")]
    public void Time_room_is_measured_from_its_shape(string time, string pattern) =>
        Assert.Equal(pattern, BoardPlan.TimePattern(time));

    [Theory]
    [InlineData(1620f, 1, "27m")]
    [InlineData(1619.5f, 1, "27m")]
    [InlineData(61f, 1, "2m")]
    [InlineData(60f, 1, null)]
    [InlineData(30f, 1, null)]
    [InlineData(299f, 5, null)]
    [InlineData(1620f, 0, null)]
    public void Minutes_only_until_seconds_under(float secondsLeft, int secondsUnder, string? text) =>
        Assert.Equal(text, BoardPlan.MinutesOnly(secondsLeft, secondsUnder));

    [Fact]
    public void No_statuses_no_sections()
    {
        Assert.Empty(Plan([], StatusLayout.Icons, split: false));
        Assert.Empty(Plan([], StatusLayout.Icons, split: true));
    }
}

public class TimeSortTests
{
    // Statuses as "group:name:seconds", or "group:name" for no time.
    private static float? TimeLeft(string effect) =>
        effect.Split(':') is { Length: 3 } parts ? float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) : null;

    private static PlannedSection<string> Section(params string[] effects)
    {
        PlannedSection<string> section = new(null);
        foreach (string effect in effects)
        {
            PlannedTile<string> tile = new(effect.Split(':')[0], false);
            tile.Members.Add(effect);
            section.Tiles.Add(tile);
        }
        return section;
    }

    private static string[] Order(PlannedSection<string> section) => section.Tiles.Select(t => t.Members[0]).ToArray();

    [Fact]
    public void Shortest_first_untimed_last_in_their_order()
    {
        PlannedSection<string> section = Section("A:Rested", "A:Mead:300", "A:Wet", "A:Food:45", "A:Kin:120");
        BoardPlan.SortByTimeLeft([section], TimeLeft, keepGroups: false);
        Assert.Equal(["A:Food:45", "A:Kin:120", "A:Mead:300", "A:Rested", "A:Wet"], Order(section));
    }

    [Fact]
    public void Keeping_groups_sorts_only_within_each_group()
    {
        PlannedSection<string> section = Section("A:Mead:300", "A:Food:45", "B:Kin:10", "B:Power:90");
        BoardPlan.SortByTimeLeft([section], TimeLeft, keepGroups: true);
        Assert.Equal(["A:Food:45", "A:Mead:300", "B:Kin:10", "B:Power:90"], Order(section));
    }

    [Fact]
    public void Stack_counts_as_its_member_ending_first()
    {
        PlannedSection<string> section = Section("A:Mead:100");
        PlannedTile<string> stack = new("B", true);
        stack.Members.AddRange(["B:Long:500", "B:Short:20"]);
        section.Tiles.Add(stack);
        BoardPlan.SortByTimeLeft([section], TimeLeft, keepGroups: false);
        Assert.Same(stack, section.Tiles[0]);
    }
}

public class LineTests
{
    private static int[] Lines(int count, int perLine)
    {
        int lines = BoardPlan.LineCount(count, perLine);
        return Enumerable.Range(0, lines).Select(line => BoardPlan.LineSize(count, lines, line)).ToArray();
    }

    [Fact]
    public void Overflow_is_shared_out_evenly()
    {
        Assert.Equal([6, 5], Lines(11, 10));
        Assert.Equal([7, 7, 7], Lines(21, 10));
        Assert.Equal([8, 7, 7], Lines(22, 10));
    }

    [Fact]
    public void Full_and_short_blocks_stay_one_line()
    {
        Assert.Equal([10], Lines(10, 10));
        Assert.Equal([3], Lines(3, 10));
        Assert.Equal([10, 10], Lines(20, 10));
        Assert.Empty(Lines(0, 10));
    }
}
