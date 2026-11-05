using BepInEx.Configuration;
using JoksterCube.StatusGroups.Common;
using JoksterCube.StatusGroups.Domain;
using UnityEngine;
using static JoksterCube.StatusGroups.Settings.Constants.Groups;

namespace JoksterCube.StatusGroups.Settings;

internal static class PluginConfig
{
    internal static ConfigEntry<StatusLayout> Layout = null!;
    internal static ConfigEntry<Toggle> SplitByGroup = null!;
    internal static ConfigEntry<Toggle> SortByGroup = null!;
    internal static ConfigEntry<Toggle> BiggestFirst = null!;
    internal static ConfigEntry<float> IconSize = null!;
    internal static ConfigEntry<int> PerLine = null!;
    internal static ConfigEntry<Toggle> ShowNames = null!;
    internal static ConfigEntry<float> NameWidth = null!;
    internal static ConfigEntry<Toggle> ShowGroupNames = null!;
    internal static ConfigEntry<Toggle> ShowGroupIcons = null!;
    internal static ConfigEntry<Toggle> StatusBoxes = null!;
    internal static ConfigEntry<Toggle> ExpandInMenu = null!;
    internal static ConfigEntry<float> FontSize = null!;
    internal static ConfigEntry<float> GroupFontSize = null!;
    internal static ConfigEntry<float> TimeFontSize = null!;
    internal static ConfigEntry<float> EndingSoonFontSize = null!;
    internal static ConfigEntry<Color> TextColor = null!;
    internal static ConfigEntry<int> SecondsUnder = null!;
    internal static ConfigEntry<Vector2> Position = null!;

    internal static ConfigEntry<string> CustomRules = null!;
    internal static ConfigEntry<Toggle> LogStatuses = null!;

    internal static void Build(ConfigFile config)
    {
        ConfigOptions.Initialize(config);

        Layout = ConfigOptions.Config(Constants.Groups.Layout.Style);
        SplitByGroup = ConfigOptions.Config(Constants.Groups.Layout.SplitByGroup);
        SortByGroup = ConfigOptions.Config(Constants.Groups.Layout.SortByGroup);
        BiggestFirst = ConfigOptions.Config(Constants.Groups.Layout.BiggestFirst);
        IconSize = ConfigOptions.Config(Constants.Groups.Layout.IconSize);
        PerLine = ConfigOptions.Config(Constants.Groups.Layout.PerLine);
        ShowNames = ConfigOptions.Config(Constants.Groups.Layout.ShowNames);
        NameWidth = ConfigOptions.Config(Constants.Groups.Layout.NameWidth);
        ShowGroupNames = ConfigOptions.Config(Constants.Groups.Layout.ShowGroupNames);
        ShowGroupIcons = ConfigOptions.Config(Constants.Groups.Layout.ShowGroupIcons);
        StatusBoxes = ConfigOptions.Config(Constants.Groups.Layout.StatusBoxes);
        ExpandInMenu = ConfigOptions.Config(Constants.Groups.Layout.ExpandInMenu);
        FontSize = ConfigOptions.Config(Constants.Groups.Layout.FontSize);
        GroupFontSize = ConfigOptions.Config(Constants.Groups.Layout.GroupFontSize);
        TimeFontSize = ConfigOptions.Config(Constants.Groups.Layout.TimeFontSize);
        EndingSoonFontSize = ConfigOptions.Config(Constants.Groups.Layout.EndingSoonFontSize);
        TextColor = ConfigOptions.Config(Constants.Groups.Layout.TextColor);
        SecondsUnder = ConfigOptions.Config(Constants.Groups.Layout.SecondsUnder);
        Position = ConfigOptions.Config(Constants.Groups.Layout.Position);

        CustomRules = ConfigOptions.Config(Rules.CustomRules);
        LogStatuses = ConfigOptions.Config(Rules.LogStatuses);

        GroupRegistry.AddBuiltIn();
        StatusClassifier.SetRules(CustomRules.Value);
        CustomRules.SettingChanged += (_, _) => StatusClassifier.SetRules(CustomRules.Value);
    }
}
