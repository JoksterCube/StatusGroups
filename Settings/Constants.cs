using BepInEx.Configuration;
using JoksterCube.StatusGroups.Common;
using JoksterCube.StatusGroups.Domain;
using UnityEngine;

namespace JoksterCube.StatusGroups.Settings;

internal static class Constants
{
    internal static class Plugin
    {
        internal const string ModName = "StatusGroups";
        internal const string ModVersion = "1.0.4";
        internal const string Author = "JoksterCube";
        internal const string ModGUID = $"{Author}.{ModName}";
        internal const string Description = "Tidy status effects: smaller icons, sorted into groups like buffs, cooldowns, negatives and rites. Pick a layout: Icons, Stacked or Vanilla.";
        internal const string Copyright = "Copyright ©  2026";
        internal const string Guid = "6f0f5d52-3a2e-4b8c-9d61-0b7a8f3e2c41";

        internal const string ConfigFileName = $"{ModGUID}.cfg";
    }

    internal static class DebugMessages
    {
        internal const string ReadConfigCalled = "ReadConfigValues called";
        internal const string ErrorLoadingConfig = $"There was an issue loading your {Plugin.ConfigFileName}";
        internal const string RequestCheckConfig = "Please check your config entries for spelling and format!";
    }

    internal static class Groups
    {
        // Every setting here works the same in Icons and Stacked; Vanilla uses none of them.
        internal static class Layout
        {
            internal const string Group = "1 - Layout";

            internal static readonly ConfigInfo<StatusLayout> Style = new(
                Group,
                "Layout",
                "How status effects are shown. Icons: small icons in rows, the time under each. Stacked: the same, but one " +
                "icon per group, with how many it holds and the time of the one ending first. Vanilla: the game's own list.",
                StatusLayout.Icons);

            internal static readonly ConfigInfo<Toggle> SplitByGroup = new(
                Group,
                "Split By Group",
                "Give each group its own block of icons, with the group's name above it (see Show Group Names). Stacked groups share one block at the start. Off: every status in one block.",
                Toggle.On);

            internal static readonly ConfigInfo<Toggle> SortByGroup = new(
                Group,
                "Sort By Group",
                "Without Split By Group: keep the statuses of a group together, in group order. Off keeps the game's order.",
                Toggle.On);

            internal static readonly ConfigInfo<Toggle> BiggestFirst = new(
                Group,
                "Longest Groups First",
                "Put the groups with the longest row first (on top with Split By Group), the shortest last. A group split over " +
                "two rows counts its longer one. Of groups as long, the one whose first status runs out latest (so it stays " +
                "that long longest) comes first, then by Order. A stacked group counts as one. Off: groups go by their Order only.",
                Toggle.On);

            internal static readonly ConfigInfo<float> IconSize = new(
                Group,
                "Icon Size",
                new ConfigDescription(
                    "Size of the status icons compared to the game's (1 is the game's size). Texts keep their Font Size and Time Font Size.",
                    new AcceptableValueRange<float>(0.3f, 1.5f)),
                0.75f);

            internal static readonly ConfigInfo<int> PerLine = new(
                Group,
                "Statuses Per Line",
                new ConfigDescription(
                    "How many statuses a row holds before the next row starts.",
                    new AcceptableValueRange<int>(1, 50)),
                18);

            internal static readonly ConfigInfo<Toggle> ShowNames = new(
                Group,
                "Show Names",
                "Show each status's name under its time. A stacked group shows the group's name.",
                Toggle.Off);

            internal static readonly ConfigInfo<float> NameWidth = new(
                Group,
                "Name Width",
                new ConfigDescription(
                    "The widest a status's name gets, in screen units. Longer names go on to the next line.",
                    new AcceptableValueRange<float>(30f, 400f)),
                30f);

            internal static readonly ConfigInfo<Toggle> ShowGroupNames = new(
                Group,
                "Show Group Names",
                "With Split By Group: show the group's name above its block.",
                Toggle.Off);

            internal static readonly ConfigInfo<Toggle> ShowGroupIcons = new(
                Group,
                "Show Group Icons",
                "With Split By Group: an icon at the right of each group's block, so you see which group is which. Pick each group's icon in the Groups settings.",
                Toggle.On);

            internal static readonly ConfigInfo<Toggle> StatusBoxes = new(
                Group,
                "Status Boxes",
                "Icons and Stacked layouts: each status in a small square with its time on the bottom of its icon, and each " +
                "group's icon (Show Group Icons) smaller, on a banner as tall as the group. Off: times under the icons, no squares.",
                Toggle.On);

            internal static readonly ConfigInfo<Toggle> ExpandInMenu = new(
                Group,
                "Expand In Menu",
                "While the menu (Esc) is open, stacked groups show every status, so you can see what they hold. They stack " +
                "again when the menu closes. Hidden groups stay hidden.",
                Toggle.On);

            internal static readonly ConfigInfo<float> FontSize = new(
                Group,
                "Font Size",
                new ConfigDescription(
                    "Size of the status names, whatever the icon size. Group names and times have their own sizes.",
                    new AcceptableValueRange<float>(8f, 32f)),
                8f);

            internal static readonly ConfigInfo<float> GroupFontSize = new(
                Group,
                "Group Font Size",
                new ConfigDescription(
                    "Size of the group names (Show Group Names), whatever the icon size.",
                    new AcceptableValueRange<float>(8f, 32f)),
                14f);

            internal static readonly ConfigInfo<float> TimeFontSize = new(
                Group,
                "Time Font Size",
                new ConfigDescription(
                    "Size of the times and stack counts, whatever the icon size. Under a minute left, a time in a Status Box grows " +
                    "(Ending Soon Font Size).",
                    new AcceptableValueRange<float>(8f, 32f)),
                16f);

            internal static readonly ConfigInfo<float> EndingSoonFontSize = new(
                Group,
                "Ending Soon Font Size",
                new ConfigDescription(
                    "Status Boxes: size of a time with under a minute left, shown big in the middle of its square. " +
                    "0: grows to fill the square.",
                    new AcceptableValueRange<float>(0f, 64f)),
                24f);

            internal static readonly ConfigInfo<Color> TextColor = new(
                Group,
                "Text Color",
                "Color of every text. Times under a minute show in red.",
                Color.white);

            internal static readonly ConfigInfo<int> SecondsUnder = new(
                Group,
                "Seconds Under",
                new ConfigDescription(
                    "Times show whole minutes only (\"27m\") until this many minutes are left, then the game's time with seconds " +
                    "(\"4:59\", \"59\"). 0: always with seconds, like the game.",
                    new AcceptableValueRange<int>(0, 60)),
                1);

            internal static readonly ConfigInfo<Vector2> Position = new(
                Group,
                "Position",
                "How far the status icons are moved from the game's spot, in screen units. Open the menu (Esc) and drag them " +
                "to move them; right-click them there to put them back.",
                new Vector2(24.75f, 26f));
        }

        internal static class GroupSettings
        {
            internal const string Group = "2 - Groups";

            internal static ConfigDescription Display(string name) => new(
                $"How the {name} group shows. Default: as the layout shows groups. Expanded: every status on its own. " +
                "Stacked: one icon for the whole group. Hidden: not shown at all.");

            internal static ConfigDescription Name(string name) => new(
                $"Your own name for the {name} group, shown above its block (Show Group Names) and on its stacked icon. " +
                "Empty: its usual name. Custom Rules still call it by its setting name.");

            internal static ConfigDescription Icon(string name) => new(
                $"The status whose icon marks the {name} group (Show Group Icons): its name as shown in game (Rested) or its " +
                "internal name. Empty: the group's own icon, or else the icon of its first status.");

            internal static ConfigDescription Order(string name) => new(
                $"Place of the {name} group: lower numbers come first.",
                new AcceptableValueRange<int>(0, 10000));
        }

        internal static class Rules
        {
            internal const string Group = "3 - Rules";

            internal static readonly ConfigInfo<string> CustomRules = new(
                Group,
                "Custom Rules",
                "Put statuses into groups yourself; these come before every other rule. A comma separated list of status=group, " +
                "where status is the status's name as shown in game (Rested) or its internal name (SE_TempestRite_CryptDelver), " +
                "* matches anything (SE_MyMod_*), and group is a group's name from the Groups settings or a new one. " +
                "Example: Wishbone=Buffs, SE_MyMod_*=My Mod",
                string.Empty);

            internal static readonly ConfigInfo<Toggle> LogStatuses = new(
                Group,
                "Log Statuses",
                "Write each status you get to the BepInEx log once, with its internal name and group. Handy for writing Custom Rules.",
                Toggle.Off) { IsAdvanced = true };
        }
    }
}
