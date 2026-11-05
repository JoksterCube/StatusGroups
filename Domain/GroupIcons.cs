using System;
using System.Collections.Generic;
using UnityEngine;

namespace JoksterCube.StatusGroups.Domain;

// The built-in groups' icons, in the style of TempestRite's rites-page tabs: flat silhouettes like the game's menu icons,
// painted pale (lighter on top, a darker rim, a soft shadow) so the board tints them off-white. Painted at 64 x 64 in code.
// Buffs: a mead bottle. Cooldowns: an hourglass. Negatives: a cracked round shield. Passive: a campfire.
internal static class GroupIcons
{
    private const int Size = 64;
    // Off-white, a little warm, so the group icons read on the dark banners; TempestRite's icons are tinted the same.
    internal static readonly Color Tint = new(0.95f, 0.92f, 0.85f, 1f);

    private static readonly Dictionary<string, Sprite> Cache = new(StringComparer.OrdinalIgnoreCase);

    // Status Boxes, when the hotbar's slot background isn't found: a grey see-through square behind each status like the
    // item slots, and a darker banner behind a group's icon. No rim. Nine-sliced, so they stretch with the same corners.
    private static Sprite? _box;
    private static Sprite? _banner;
    internal static Sprite BoxSprite => _box ? _box! : _box = Panel("StatusGroups_Box", new Color(0.2f, 0.2f, 0.2f, 0.5f));
    internal static Sprite BannerSprite => _banner ? _banner! : _banner = Panel("StatusGroups_Banner", new Color(0.12f, 0.12f, 0.12f, 0.6f));
    // Pixels of the panels per screen unit: their corners and rims at about two thirds of their pixel size.
    internal const float BackDensity = 1.5f;

    internal static bool Has(string groupId) => ShapeOf(groupId) != null;

    internal static Sprite? Get(string groupId)
    {
        if (Cache.TryGetValue(groupId, out Sprite sprite) && sprite) return sprite;
        if (Render(groupId) is not { } colors) return null;
        sprite = ToSprite(colors, $"StatusGroups_{groupId}");
        Cache[groupId] = sprite;
        return sprite;
    }

    // The icon's 64 x 64 colors, row 0 at the top (previews); null for a group without one.
    internal static Color[]? Render(string groupId)
    {
        Func<float, float, float>? shape = ShapeOf(groupId);
        return shape == null ? null : Emblem(shape);
    }

    private static Func<float, float, float>? ShapeOf(string groupId) => groupId switch
    {
        GroupRegistry.Buffs => BottleShape,
        GroupRegistry.Cooldowns => HourglassShape,
        GroupRegistry.Negatives => ShieldShape,
        GroupRegistry.Passive => CampfireShape,
        _ => null
    };

    // As TempestRite's WeatherIcons.Emblem.
    private static Color[] Emblem(Func<float, float, float> shape)
    {
        Color[] pixels = new Color[Size * Size];
        float[] alpha = new float[Size * Size];
        float[] distance = new float[Size * Size];
        for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                int i = py * Size + px;
                distance[i] = shape(px + 0.5f, py + 0.5f);
                alpha[i] = Mathf.Clamp01(0.5f - distance[i]);
            }

        // The shadow falls a pixel down.
        float[] shadow = Blur(alpha, 1);
        for (int i = Size; i < shadow.Length; i++) pixels[i] = Over(pixels[i], new Color(0f, 0f, 0f, shadow[i - Size] * 0.7f));

        for (int i = 0; i < alpha.Length; i++)
        {
            if (alpha[i] <= 0f) continue;
            float y = i / Size + 0.5f;
            float light = Mathf.Lerp(1f, 0.78f, Mathf.Clamp01((y - 8f) / 48f));
            light *= 0.8f + 0.2f * Mathf.Clamp01(-distance[i] / 2.5f);
            pixels[i] = Over(pixels[i], new Color(light, light, light, alpha[i]));
        }
        return pixels;
    }

    // ---- Shapes: distance functions (negative inside), in pixels with y going down ----

    // A round-bellied mead bottle with a neck and a lip, a band cut where the neck meets the belly and a gleam on it.
    private static float BottleShape(float x, float y)
    {
        float body = Circle(x, y, 32f, 40f, 20.5f);
        float neck = Box(x, y, 25.5f, 8f, 38.5f, 24f, 1.5f);
        float lip = Box(x, y, 22f, 3f, 42f, 9.5f, 2.5f);
        float bottle = Mathf.Min(Mathf.Min(body, neck), lip);
        float band = Capsule(x, y, 20f, 24f, 44f, 24f, 1.3f);
        // A quarter arc on the belly's upper left.
        float gleam = Mathf.Max(Mathf.Max(Mathf.Abs(Circle(x, y, 32f, 40f, 14f)) - 1.6f, x - 29f), y - 39f);
        return Mathf.Max(bottle, -Mathf.Min(band, gleam));
    }

    private static readonly Vector2[] GlassTop = [new(17f, 11f), new(47f, 11f), new(34f, 32f), new(30f, 32f)];
    private static readonly Vector2[] GlassBottom = [new(30f, 32f), new(34f, 32f), new(47f, 53f), new(17f, 53f)];
    private static readonly Vector2[] SandTop = [new(25f, 22f), new(39f, 22f), new(32f, 31f)];
    private static readonly Vector2[] SandBottom = [new(32f, 41f), new(42f, 53f), new(22f, 53f)];

    // An hourglass: a frame top and bottom, the glass drawn as an outline, sand left in the top and heaped in the bottom.
    private static float HourglassShape(float x, float y)
    {
        float frame = Mathf.Min(Box(x, y, 11f, 4f, 53f, 11f, 2f), Box(x, y, 11f, 53f, 53f, 60f, 2f));
        float glass = Mathf.Abs(Mathf.Min(Polygon(x, y, GlassTop), Polygon(x, y, GlassBottom))) - 1.8f;
        float sand = Mathf.Min(Polygon(x, y, SandTop), Polygon(x, y, SandBottom));
        float trickle = Capsule(x, y, 32f, 31f, 32f, 42f, 0.9f);
        return Mathf.Min(Mathf.Min(frame, glass), Mathf.Min(sand, trickle));
    }

    // A round shield with its rim ring and boss, split by a crack from top to bottom.
    private static float ShieldShape(float x, float y)
    {
        float shield = Circle(x, y, 32f, 32f, 26f);
        float rim = Mathf.Abs(Circle(x, y, 32f, 32f, 20f)) - 1.1f;
        float boss = Mathf.Abs(Circle(x, y, 32f, 32f, 7f)) - 1.1f;
        float crack = Mathf.Min(
            Mathf.Min(Capsule(x, y, 29f, 4f, 36f, 17f, 1.7f), Capsule(x, y, 36f, 17f, 27f, 29f, 1.7f)),
            Mathf.Min(Capsule(x, y, 27f, 29f, 37f, 43f, 1.7f), Capsule(x, y, 37f, 43f, 31f, 61f, 1.7f)));
        return Mathf.Max(shield, -Mathf.Min(Mathf.Min(rim, boss), crack));
    }

    // A campfire: a flame with a hollow heart over two crossed logs, a gap cut between flame and logs.
    private static float CampfireShape(float x, float y)
    {
        float flame = Flame(x, y, 32f, 37f, 13f, 5f);
        float heart = Flame(x, y, 32f, 41f, 5.5f, 25f);
        flame = Mathf.Max(flame, -heart);
        float logs = Mathf.Min(Capsule(x, y, 11f, 57f, 53f, 46f, 4f), Capsule(x, y, 11f, 46f, 53f, 57f, 4f));
        logs = Mathf.Max(logs, -(Flame(x, y, 32f, 37f, 13f, 5f) - 2.5f));
        return Mathf.Min(flame, logs);
    }

    // A teardrop: a round bottom of radius r centred at (cx, cy), drawn up to a point at height tip.
    private static float Flame(float x, float y, float cx, float cy, float r, float tip)
    {
        float round = Circle(x, y, cx, cy, r);
        // Above the round: its sides narrow in a curve to the tip.
        if (y >= cy) return round;
        float t = Mathf.Clamp01((cy - y) / (cy - tip));
        float half = r * Mathf.Sqrt(1f - t) * (1f - 0.25f * t);
        float sway = Mathf.Sin(t * Mathf.PI) * r * 0.15f;
        float side = Mathf.Abs(x - cx - sway) - half;
        return Mathf.Max(Mathf.Min(round, side), tip - y);
    }

    private static float Box(float x, float y, float x0, float y0, float x1, float y1, float r = 0f)
    {
        float qx = Mathf.Abs(x - (x0 + x1) / 2f) - ((x1 - x0) / 2f - r);
        float qy = Mathf.Abs(y - (y0 + y1) / 2f) - ((y1 - y0) / 2f - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static float Circle(float x, float y, float cx, float cy, float r) =>
        Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

    private static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
    {
        float dx = bx - ax, dy = by - ay;
        float length = dx * dx + dy * dy;
        float t = length > 0f ? Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / length) : 0f;
        float px = ax + t * dx - x, py = ay + t * dy - y;
        return Mathf.Sqrt(px * px + py * py) - r;
    }

    private static float Polygon(float x, float y, Vector2[] points)
    {
        float d = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            Vector2 a = points[j], b = points[i];
            d = Math.Min(d, Capsule(x, y, a.x, a.y, b.x, b.y, 0f));
            if ((b.y > y) != (a.y > y) && x < (a.x - b.x) * (y - b.y) / (a.y - b.y) + b.x) inside = !inside;
        }
        return inside ? -d : d;
    }

    // ---- Drawing ----

    private static Color Over(Color below, Color above)
    {
        float a = above.a + below.a * (1f - above.a);
        if (a <= 0f) return Color.clear;
        float k = below.a * (1f - above.a);
        return new Color(
            (above.r * above.a + below.r * k) / a,
            (above.g * above.a + below.g * k) / a,
            (above.b * above.a + below.b * k) / a,
            a);
    }

    // Two box blurs: close enough to a soft shadow.
    private static float[] Blur(float[] values, int radius)
    {
        float[] result = values;
        for (int pass = 0; pass < 2; pass++)
        {
            float[] horizontal = new float[result.Length];
            float[] vertical = new float[result.Length];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++) sum += result[y * Size + Mathf.Clamp(x + k, 0, Size - 1)];
                    horizontal[y * Size + x] = sum / (2 * radius + 1);
                }
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++) sum += horizontal[Mathf.Clamp(y + k, 0, Size - 1) * Size + x];
                    vertical[y * Size + x] = sum / (2 * radius + 1);
                }
            result = vertical;
        }
        return result;
    }

    // A rounded square, 24 pixels, its corners kept when sliced.
    private static Sprite Panel(string name, Color fill)
    {
        const int PanelSize = 24;
        const float Radius = 5f;
        Color[] pixels = new Color[PanelSize * PanelSize];
        for (int py = 0; py < PanelSize; py++)
            for (int px = 0; px < PanelSize; px++)
            {
                float d = Box(px + 0.5f, py + 0.5f, 0f, 0f, PanelSize, PanelSize, Radius);
                Color color = fill;
                color.a *= Mathf.Clamp01(0.5f - d);
                pixels[py * PanelSize + px] = color;
            }
        Texture2D texture = new(PanelSize, PanelSize, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, PanelSize, PanelSize), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite ToSprite(Color[] pixels, string name)
    {
        Color[] flipped = new Color[pixels.Length];
        // Textures start at the bottom row.
        for (int y = 0; y < Size; y++) Array.Copy(pixels, y * Size, flipped, (Size - 1 - y) * Size, Size);
        Texture2D texture = new(Size, Size, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(flipped);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
