using System;
using System.IO;
using JoksterCube.StatusGroups.Domain;
using UnityEngine;
using Xunit;

namespace StatusGroups.Tests;

// Renders the built-in groups' icons for previews (Tools/Icon/group_icons.py). Runs only when STATUSGROUPS_ICON_OUT is
// set; otherwise it does nothing. .rgba: width and height (int32), then width x height RGBA bytes, row 0 at the top.
public class IconPreview
{
    [Fact]
    public void Render_group_icons()
    {
        string? output = Environment.GetEnvironmentVariable("STATUSGROUPS_ICON_OUT");
        if (string.IsNullOrEmpty(output)) return;
        Directory.CreateDirectory(output);
        foreach (string group in new[] { GroupRegistry.Buffs, GroupRegistry.Cooldowns, GroupRegistry.Negatives, GroupRegistry.Passive })
        {
            Color[] colors = GroupIcons.Render(group)!;
            using BinaryWriter writer = new(File.Create(Path.Combine(output, group + ".rgba")));
            writer.Write(64);
            writer.Write(64);
            foreach (Color c in colors)
            {
                writer.Write(Byte(c.r));
                writer.Write(Byte(c.g));
                writer.Write(Byte(c.b));
                writer.Write(Byte(c.a));
            }
        }
    }

    private static byte Byte(float value) => (byte)Math.Round(Math.Clamp(value, 0f, 1f) * 255f);
}
