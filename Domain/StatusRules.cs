using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace JoksterCube.StatusGroups.Domain;

// The player's Custom Rules: "status=group" pairs, comma separated, where status may hold * for anything.
// A status matches by its internal name (SE_TempestRite_CryptDelver) or the name shown in game (Rested), ignoring case.
internal sealed class StatusRules
{
    private readonly List<(Regex Pattern, string Group)> _rules = [];

    internal int Count => _rules.Count;

    internal static StatusRules Parse(string? text)
    {
        StatusRules rules = new();
        if (string.IsNullOrWhiteSpace(text)) return rules;
        foreach (string part in text!.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = part.IndexOf('=');
            if (equals <= 0) continue;
            string pattern = part.Substring(0, equals).Trim();
            string group = part.Substring(equals + 1).Trim();
            if (pattern.Length == 0 || group.Length == 0) continue;
            rules._rules.Add((Glob(pattern), group));
        }
        return rules;
    }

    // The group of the first rule that matches either name, or null.
    internal string? GroupOf(string internalName, string shownName)
    {
        foreach ((Regex pattern, string group) in _rules)
            if (pattern.IsMatch(internalName) || (shownName.Length > 0 && pattern.IsMatch(shownName))) return group;
        return null;
    }

    private static Regex Glob(string pattern) =>
        new("^" + Regex.Escape(pattern).Replace(@"\*", ".*") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
