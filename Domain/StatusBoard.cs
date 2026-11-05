using System;
using System.Collections.Generic;
using JoksterCube.StatusGroups.Common;
using JoksterCube.StatusGroups.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JoksterCube.StatusGroups.Domain;

// Draws the status effects in place of the game's list (every layout but Vanilla). Each icon is a copy of the game's
// own status template, so it looks and animates the same (cooldown cross, flashing, "new" flash), only smaller.
// Names and times are the board's own texts, not the template's, so they keep Font Size and Time Font Size whatever
// the icon size.
// The copies live under one object next to the game's list and are reused from frame to frame. What goes where is
// planned again only when the statuses or a setting change; icons, times and positions update every frame.
// While the menu (Esc) is open a backdrop shows behind the icons and the whole board can be dragged (BoardMover).
internal static class StatusBoard
{
    private const string Feature = "Status layout";
    private const string RootName = "StatusGroups_Board";
    // The game's list starts this far from the right.
    private const float RightPad = 4f;
    // In screen units: between icons (at full size), between groups' blocks, and between a block and its group's name.
    private const float IconGap = 4f;
    private const float SectionGap = 8f;
    private const float CaptionGap = 2f;
    // A group's icon (Show Group Icons): its size against the status icons, and its gap to the block.
    private const float MarkerScale = 0.8f;
    private const float MarkerGap = 6f;
    // Status Boxes: room around an icon in its square, against the icon's size (the icon fills about 3/4 of the square,
    // like an item in a hotbar slot), and the group's icon on its banner, against a status icon.
    private const float BoxPad = 0.14f;
    // A text on a square (time or a status's own text) is shrunk to fit this much of the square's width.
    private const float BoxTextFill = 0.92f;
    private const float BannerIconScale = 0.6f;
    private static readonly Color FlashColor = new(1f, 0.5f, 0.5f, 1f);
    // A time with less than this many seconds left shows in this color.
    private const float EndingSoon = 60f;
    private static readonly Color EndingSoonColor = new(1f, 0.25f, 0.25f, 1f);
    // The backdrop shown while the board can be moved, and its border.
    private static readonly Color HandleColor = new(0f, 0f, 0f, 0.4f);
    private static readonly Color HandleBorder = new(1f, 0.8f, 0.45f, 0.6f);
    private const float HandlePad = 4f;

    private static Hud? _hud;
    private static RectTransform? _root;
    private static Metrics? _metrics;
    private static BoardMover? _mover;
    private static RectTransform? _handle;
    // Labels[i] holds the name and time of Tiles[i].
    private static readonly List<TileView> Tiles = [];
    private static readonly List<Label> Labels = [];
    private static readonly List<Caption> Captions = [];
    private static readonly List<TileView> Markers = [];
    // Status Boxes' squares and banners, under one object kept behind the icons.
    private static RectTransform? _backs;
    private static readonly List<Image> Backs = [];
    private static SlotLook? _slot;
    // Statuses found by name for groups' icons, for this item list.
    private static readonly Dictionary<string, StatusEffect?> IconStatuses = new(StringComparer.OrdinalIgnoreCase);
    private static ObjectDB? _iconDb;
    private static int _iconDbCount = -1;

    // The plan, and what it was made from.
    private static List<PlannedSection<StatusEffect>> _plan = [];
    private static readonly List<StatusEffect> PlannedFor = [];
    private static int _planKey = int.MinValue;

    // The settings every layout draws with, read once a frame.
    private struct Style
    {
        internal float Scale;
        internal int PerLine;
        internal bool Names;
        internal float NameWidth;
        internal bool Captions;
        internal bool Markers;
        internal bool Boxes;
        internal float FontSize;
        internal float GroupFontSize;
        internal float TimeFontSize;
        internal float EndingSoonFontSize;
        internal Color Color;
    }

    // From the Hud.UpdateStatusEffects prefix. False: drawn here, the game's list stays empty.
    internal static bool Draw(Hud hud, List<StatusEffect> effects)
    {
        if (PatchGuard.IsDisabled(Feature)) return true;
        StatusLayout layout = PluginConfig.Layout.Value;
        if (layout == StatusLayout.Vanilla)
        {
            if (_root) Dispose();
            return true;
        }
        try
        {
            Render(hud, effects, layout);
            return false;
        }
        catch (Exception exception)
        {
            PatchGuard.Fail(Feature, exception);
            Dispose();
            return true;
        }
    }

    internal static void Dispose()
    {
        if (_root) Object.Destroy(_root!.gameObject);
        _root = null;
        _hud = null;
        _metrics = null;
        _mover = null;
        _handle = null;
        Tiles.Clear();
        Labels.Clear();
        Captions.Clear();
        Markers.Clear();
        Backs.Clear();
        _backs = null;
        _slot = null;
        _plan = [];
        PlannedFor.Clear();
        _planKey = int.MinValue;
    }

    private static void Render(Hud hud, List<StatusEffect> effects, StatusLayout layout)
    {
        if (_hud != hud || !_root) Create(hud);
        RemoveGameList(hud);
        if (!BoardMover.CanMove) _mover!.Finish();
        if (!_mover!.Dragging) SetPosition(_root!, PluginConfig.Position.Value);
        Player? player = Player.m_localPlayer;
        if (!player) return;

        bool sort = PluginConfig.SortByGroup.IsOn();
        bool split = PluginConfig.SplitByGroup.IsOn();
        bool biggest = PluginConfig.BiggestFirst.IsOn();
        int perLine = Mathf.Max(1, PluginConfig.PerLine.Value);
        // Expand In Menu: while the menu (Esc) is open, stacked groups show every status.
        bool expand = PluginConfig.ExpandInMenu.IsOn() && Menu.IsVisible();
        int key = PlanKey(layout, sort, split, biggest, expand) * 31 + perLine;
        if (key != _planKey || !SameEffects(effects))
        {
            _planKey = key;
            PlannedFor.Clear();
            PlannedFor.AddRange(effects);
            _plan = BoardPlan.Plan(effects, effect =>
            {
                StatusGroup group = StatusClassifier.GroupOf(effect, player!);
                GroupDisplay display = BoardPlan.Resolve(group.Display.Value, layout);
                if (expand && display == GroupDisplay.Stacked) display = GroupDisplay.Expanded;
                return new PlannedGroup(group.Id, group.Order.Value, display);
            }, split, sort, biggest, TimeLeft, perLine);
            // A rule's group made while planning changes the version: don't plan again next frame for that.
            _planKey = PlanKey(layout, sort, split, biggest, expand) * 31 + perLine;
        }
        // Group runs only matter in one shared block sorted by group; split blocks hold one group (or stacks).
        BoardPlan.SortByTimeLeft(_plan, TimeLeft, sort && !split, StatusClassifier.IsWorn);

        Style style = new()
        {
            Scale = PluginConfig.IconSize.Value,
            PerLine = Mathf.Max(1, PluginConfig.PerLine.Value),
            Names = PluginConfig.ShowNames.IsOn(),
            NameWidth = PluginConfig.NameWidth.Value,
            Captions = split && PluginConfig.ShowGroupNames.IsOn(),
            Markers = split && PluginConfig.ShowGroupIcons.IsOn(),
            Boxes = PluginConfig.StatusBoxes.IsOn(),
            FontSize = PluginConfig.FontSize.Value,
            GroupFontSize = PluginConfig.GroupFontSize.Value,
            TimeFontSize = PluginConfig.TimeFontSize.Value,
            EndingSoonFontSize = PluginConfig.EndingSoonFontSize.Value,
            Color = PluginConfig.TextColor.Value
        };
        PlaceIcons(style);
    }

    private static float? TimeLeft(StatusEffect effect) => effect.m_ttl > 0f ? effect.GetRemaningTime() : null;

    private static int PlanKey(StatusLayout layout, bool sort, bool split, bool biggest, bool expand) =>
        (((((int)layout * 2 + (sort ? 1 : 0)) * 2 + (split ? 1 : 0)) * 2 + (biggest ? 1 : 0)) * 2 + (expand ? 1 : 0)) * 7919
        + StatusClassifier.Version;

    private static bool SameEffects(List<StatusEffect> effects)
    {
        if (effects.Count != PlannedFor.Count) return false;
        for (int i = 0; i < effects.Count; i++)
            if (!ReferenceEquals(effects[i], PlannedFor[i])) return false;
        return true;
    }

    // Shows every planned tile and its texts, measured, ready to be placed.
    private static int ShowAll(Style style)
    {
        int index = 0;
        foreach (PlannedSection<StatusEffect> section in _plan)
            foreach (PlannedTile<StatusEffect> planned in section.Tiles)
            {
                Show(Tile(index), planned, style, out string name, out string? time, out Color timeColor, out bool endingSoon);
                LabelAt(index).Set(style.Names ? name : null, time, style.FontSize, style.TimeFontSize, style.Color, timeColor,
                    endingSoon ? style.EndingSoonFontSize : null);
                index++;
            }
        return index;
    }

    // Icons and Stacked layouts: right-aligned rows of icons read left to right, each with its time and name under it, blocks from top
    // to bottom. Every cell on the board is as wide as the widest icon, time or name (up to Name Width), measured from the
    // times' shape rather than their digits, so nothing moves while the times run down.
    // Status Boxes: each icon in a small square with its time on the icon's bottom, and each group's icon, smaller, at the
    // middle of a banner as tall as the group.
    private static void PlaceIcons(Style style)
    {
        Rect icon = _metrics!.Icon;
        float scale = style.Scale;
        float iconWidth = icon.width * scale;
        float iconHeight = icon.height * scale;
        int count = ShowAll(style);
        bool boxes = style.Boxes;
        float pad = boxes ? BoxPad * iconWidth : 0f;
        float boxWidth = iconWidth + pad * 2f;
        float boxHeight = iconHeight + pad * 2f;

        float cellWidth = boxWidth;
        float timeHeight = 0f;
        for (int i = 0; i < count; i++)
        {
            Label label = Labels[i];
            // In Status Boxes the time sits on the square and is shrunk to fit it, so it doesn't widen the cell.
            cellWidth = Mathf.Max(cellWidth, boxes ? 0f : label.TimeWidth, Mathf.Min(label.NameWidth, style.NameWidth));
            // Room for a time under every icon, so rows keep their height as timed and untimed statuses come and go.
            timeHeight = Mathf.Max(timeHeight, label.TimeHeight);
        }
        float stepX = cellWidth + IconGap * scale;
        float edge = -RightPad + icon.xMax * scale;
        // Groups' icons get a column of their own on the right; every block keeps to its left, stacks too.
        float markerScale = scale * (boxes ? BannerIconScale : MarkerScale);
        float markerSize = icon.width * markerScale;
        float bannerWidth = boxes ? markerSize + pad * 2f : markerSize;
        bool markers = style.Markers && _plan.Exists(section => section.Caption != null && section.Tiles.Count > 0);
        float right = markers ? edge - bannerWidth - MarkerGap : edge;
        float top = icon.yMax * scale;

        Rect bounds = new();
        bool any = false;
        int index = 0;
        int captionIndex = 0;
        int markerIndex = 0;
        int backIndex = 0;
        foreach (PlannedSection<StatusEffect> section in _plan)
        {
            int tiles = section.Tiles.Count;
            if (tiles == 0) continue;
            int lines = BoardPlan.LineCount(tiles, style.PerLine);
            if (style.Captions && section.Caption != null)
            {
                Caption caption = CaptionAt(captionIndex++, section.Caption, style, TextAlignmentOptions.BottomRight);
                int columns = BoardPlan.LineSize(tiles, lines, 0);
                float width = Mathf.Max(columns * stepX - IconGap * scale, caption.Width);
                float bottom = top - caption.Height;
                caption.Place(new Vector2(right, bottom), new Vector2(width, caption.Height), pivotX: 1f);
                Grow(ref bounds, ref any, Rect.MinMaxRect(right - width, bottom, right, top));
                top = bottom - CaptionGap;
            }
            float sectionTop = top;

            for (int line = 0; line < lines; line++)
            {
                int inRow = BoardPlan.LineSize(tiles, lines, line);
                float nameHeight = 0f;
                if (style.Names)
                    for (int i = 0; i < inRow; i++) nameHeight = Mathf.Max(nameHeight, Labels[index + i].Height(cellWidth));
                float rowHeight = boxes ? boxHeight + nameHeight : iconHeight + timeHeight + nameHeight;

                for (int i = 0; i < inRow; i++)
                {
                    // Rows stay against the right edge but read left to right: the first (least time left) is leftmost.
                    float cellRight = right - (inRow - 1 - i) * stepX;
                    float center = cellRight - cellWidth * 0.5f;
                    PlaceTile(Tiles[index], new Vector2(center - icon.center.x * scale, top - pad - icon.yMax * scale), scale);
                    if (boxes)
                    {
                        PlaceBack(BackAt(backIndex++, banner: false),
                            Rect.MinMaxRect(center - boxWidth * 0.5f, top - boxHeight, center + boxWidth * 0.5f, top));
                        Labels[index].PlaceOn(center, top - boxHeight, cellWidth, timeHeight, nameHeight, boxWidth, boxHeight);
                    }
                    else Labels[index].PlaceUnder(center, top - iconHeight, cellWidth, timeHeight, nameHeight);
                    Grow(ref bounds, ref any, Rect.MinMaxRect(cellRight - cellWidth, top - rowHeight, cellRight, top));
                    index++;
                }
                top -= rowHeight + IconGap * scale;
            }
            float sectionBottom = top + IconGap * scale;

            if (markers && section.Caption != null)
            {
                Vector2 center;
                if (boxes)
                {
                    // A banner down the group's whole height, its icon in the middle.
                    Rect banner = Rect.MinMaxRect(edge - bannerWidth, sectionBottom, edge, sectionTop);
                    PlaceBack(BackAt(backIndex++, banner: true), banner);
                    Grow(ref bounds, ref any, banner);
                    center = banner.center;
                }
                // Beside the block's first row, level with its icons.
                else center = new Vector2(edge - markerSize * 0.5f, sectionTop - iconHeight * 0.5f);
                PlaceMarker(MarkerAt(markerIndex++), section, center, markerScale);
                Grow(ref bounds, ref any, Rect.MinMaxRect(center.x - markerSize * 0.5f, center.y - markerSize * 0.5f,
                    center.x + markerSize * 0.5f, center.y + markerSize * 0.5f));
            }
            top -= SectionGap;
        }

        HideRest(index, captionIndex, markerIndex, backIndex);
        PlaceHandle(any && (BoardMover.CanMove || _mover!.Dragging), bounds, HandlePad);
    }

    private static void PlaceTile(TileView tile, Vector2 position, float scale)
    {
        SetPosition(tile.Root, position);
        Vector3 scaled = Vector3.one * scale;
        if (tile.Root.localScale != scaled) tile.Root.localScale = scaled;
    }

    private static void HideRest(int tileIndex, int captionIndex, int markerIndex, int backIndex)
    {
        for (int i = backIndex; i < Backs.Count; i++)
            if (Backs[i].gameObject.activeSelf) Backs[i].gameObject.SetActive(false);
        for (int i = markerIndex; i < Markers.Count; i++)
            if (Markers[i].Root.gameObject.activeSelf) Markers[i].Root.gameObject.SetActive(false);
        for (int i = tileIndex; i < Tiles.Count; i++)
            if (Tiles[i].Root.gameObject.activeSelf) Tiles[i].Root.gameObject.SetActive(false);
        for (int i = tileIndex; i < Labels.Count; i++) Labels[i].Hide();
        for (int i = captionIndex; i < Captions.Count; i++) Captions[i].Hide();
    }

    private static void Grow(ref Rect bounds, ref bool any, Rect part)
    {
        bounds = any ? Rect.MinMaxRect(Mathf.Min(bounds.xMin, part.xMin), Mathf.Min(bounds.yMin, part.yMin),
            Mathf.Max(bounds.xMax, part.xMax), Mathf.Max(bounds.yMax, part.yMax)) : part;
        any = true;
    }

    // The backdrop around everything shown, so the board can be grabbed anywhere and is seen to be movable.
    private static void PlaceHandle(bool show, Rect bounds, float pad)
    {
        RectTransform handle = _handle!;
        if (handle.gameObject.activeSelf != show) handle.gameObject.SetActive(show);
        if (!show) return;
        SetPosition(handle, new Vector2(bounds.xMin - pad, bounds.yMin - pad));
        Vector2 size = new(bounds.width + pad * 2f, bounds.height + pad * 2f);
        if (handle.sizeDelta != size) handle.sizeDelta = size;
    }

    // A group's icon, its middle at center: the status named in its Icon setting, else its own (painted, or its mod's),
    // else its first status's. A status's cooldown cross comes along, so a group of waits looks like one.
    private static void PlaceMarker(TileView tile, PlannedSection<StatusEffect> section, Vector2 center, float scale)
    {
        Sprite? sprite = null;
        bool cooldown = false;
        bool tinted = false;
        StatusGroup group = GroupRegistry.Get(section.Caption!);
        string wanted = group.Icon.Value.Trim();
        if (wanted.Length > 0 && FindStatus(wanted) is { } named && named.m_icon)
        {
            sprite = named.m_icon;
            cooldown = named.m_cooldownIcon;
        }
        if (!sprite && group.IconSource != null)
        {
            try { sprite = group.IconSource(); }
            catch (Exception) { sprite = null; }
            tinted = sprite && group.IconTinted;
        }
        if (!sprite)
        {
            StatusEffect first = section.Tiles[0].Members[0];
            sprite = first.m_icon;
            cooldown = first.m_cooldownIcon;
        }

        if (tile.Icon.sprite != sprite) tile.Icon.sprite = sprite;
        Color color = tinted ? GroupIcons.Tint : Color.white;
        if (tile.Icon.color != color) tile.Icon.color = color;
        if (tile.Cooldown && tile.Cooldown!.activeSelf != cooldown) tile.Cooldown.SetActive(cooldown);
        if (tile.Name && tile.Name!.gameObject.activeSelf) tile.Name.gameObject.SetActive(false);
        if (tile.Time && tile.Time!.gameObject.activeSelf) tile.Time.gameObject.SetActive(false);
        if (tile.Count && tile.Count!.gameObject.activeSelf) tile.Count.gameObject.SetActive(false);
        if (!tile.Root.gameObject.activeSelf) tile.Root.gameObject.SetActive(true);
        Rect icon = _metrics!.Icon;
        PlaceTile(tile, center - icon.center * scale, scale);
    }

    // A status from the game's list (or a mod's) by its internal name or its name in game, ignoring case.
    private static StatusEffect? FindStatus(string name)
    {
        ObjectDB db = ObjectDB.instance;
        if (!db) return null;
        if (db != _iconDb || db.m_StatusEffects.Count != _iconDbCount)
        {
            IconStatuses.Clear();
            _iconDb = db;
            _iconDbCount = db.m_StatusEffects.Count;
        }
        if (IconStatuses.TryGetValue(name, out StatusEffect? found)) return found;
        found = null;
        foreach (StatusEffect effect in db.m_StatusEffects)
            if (effect && string.Equals(effect.name, name, StringComparison.OrdinalIgnoreCase))
            {
                found = effect;
                break;
            }
        if (found == null)
            foreach (StatusEffect effect in db.m_StatusEffects)
                if (effect && string.Equals(StatusClassifier.Shown(effect), name, StringComparison.OrdinalIgnoreCase))
                {
                    found = effect;
                    break;
                }
        IconStatuses[name] = found;
        return found;
    }

    // One icon: the status itself, or for a stacked group the status ending first, with how many the group holds.
    // Its name (the group's, when stacked) and time are given back for the board's own texts.
    private static void Show(TileView tile, PlannedTile<StatusEffect> planned, Style style, out string name, out string? time,
        out Color timeColor, out bool endingSoon)
    {
        StatusEffect shown = planned.Stacked ? EndingFirst(planned.Members) : planned.Members[0];

        if (tile.Icon.sprite != shown.m_icon) tile.Icon.sprite = shown.m_icon;
        bool flash = false;
        bool isNew = false;
        foreach (StatusEffect member in planned.Members)
        {
            flash |= member.m_flashIcon;
            isNew |= member.m_isNew;
        }
        Color color = flash && Mathf.Sin(Time.time * 10f) > 0f ? FlashColor : Color.white;
        if (tile.Icon.color != color) tile.Icon.color = color;
        if (tile.Cooldown && tile.Cooldown!.activeSelf != shown.m_cooldownIcon) tile.Cooldown.SetActive(shown.m_cooldownIcon);

        name = planned.Stacked ? GroupRegistry.Get(planned.GroupId).Name : StatusClassifier.Shown(shown);
        time = shown.GetIconText();
        // Only the game's plain countdown is shortened: a status writing its own text keeps it.
        float countdown = shown.m_ttl - shown.GetDuration();
        bool plain = shown.m_ttl > 0f && time == StatusEffect.GetTimeString(countdown);
        if (plain && BoardPlan.MinutesOnly(countdown, PluginConfig.SecondsUnder.Value) is { } minutes)
            time = minutes;
        if (string.IsNullOrEmpty(time)) time = null;
        // Times under a minute show in red (and big, in Status Boxes).
        endingSoon = time != null && TimeLeft(shown) is { } left && left < EndingSoon;
        // Big in a Status Box: plain seconds ("60", not "1:00").
        if (endingSoon && plain && style.Boxes) time = Mathf.CeilToInt(Mathf.Max(0f, countdown)).ToString();
        timeColor = endingSoon ? EndingSoonColor : style.Color;
        // The template's own texts shrink with the icon: the board draws its own instead.
        if (tile.Name && tile.Name!.gameObject.activeSelf) tile.Name.gameObject.SetActive(false);
        if (tile.Time && tile.Time!.gameObject.activeSelf) tile.Time.gameObject.SetActive(false);
        if (tile.Count)
        {
            if (tile.Count!.gameObject.activeSelf != planned.Stacked) tile.Count.gameObject.SetActive(planned.Stacked);
            if (planned.Stacked)
            {
                SetText(tile.Count, planned.Members.Count.ToString(), style.Color);
                // In the board's font, like the times.
                if (_metrics!.Font && tile.Count.font != _metrics.Font)
                {
                    tile.Count.font = _metrics.Font;
                    if (_metrics.FontMaterial) tile.Count.fontSharedMaterial = _metrics.FontMaterial;
                }
                // Inside the scaled icon: undo the icon's scale so it shows at Time Font Size.
                float size = style.TimeFontSize /Mathf.Max(0.01f, style.Scale);
                if (!Mathf.Approximately(tile.Count.fontSize, size)) tile.Count.fontSize = size;
            }
        }

        if (!tile.Root.gameObject.activeSelf) tile.Root.gameObject.SetActive(true);
        if (isNew)
        {
            foreach (StatusEffect member in planned.Members) member.m_isNew = false;
            if (tile.Animator) tile.Animator!.SetTrigger("flash");
        }
    }

    // The status with a time that runs out first; a group with no timed statuses shows its first.
    private static StatusEffect EndingFirst(List<StatusEffect> members)
    {
        StatusEffect best = members[0];
        float bestLeft = float.MaxValue;
        foreach (StatusEffect member in members)
        {
            if (member.m_ttl <= 0f) continue;
            float left = member.GetRemaningTime();
            if (left < bestLeft)
            {
                bestLeft = left;
                best = member;
            }
        }
        return best;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text.text != value) text.text = value;
    }

    private static void SetText(TMP_Text text, string value, Color color)
    {
        SetText(text, value);
        if (text.color != color) text.color = color;
    }

    private static void SetPosition(RectTransform rect, Vector2 position)
    {
        if (rect.anchoredPosition != position) rect.anchoredPosition = position;
    }

    private static void SetSize(RectTransform rect, Vector2 size)
    {
        if (rect.sizeDelta != size) rect.sizeDelta = size;
    }

    private static void SetAlignment(TMP_Text text, TextAlignmentOptions alignment)
    {
        if (text.alignment != alignment) text.alignment = alignment;
    }

    // True when the size changed, so the text must be measured again.
    private static bool SetFontSize(TMP_Text text, float size)
    {
        if (Mathf.Approximately(text.fontSize, size)) return false;
        text.fontSize = size;
        return true;
    }

    // Rounded up a little, so a text given exactly its width doesn't wrap.
    private static Vector2 Measure(TMP_Text text, string value)
    {
        Vector2 size = text.GetPreferredValues(value, float.PositiveInfinity, float.PositiveInfinity);
        return new Vector2(Mathf.Ceil(size.x) + 1f, Mathf.Ceil(size.y));
    }

    // The game's list is emptied once a layout takes over; it fills itself again when Vanilla is picked.
    private static void RemoveGameList(Hud hud)
    {
        if (hud.m_statusEffects.Count == 0) return;
        foreach (RectTransform entry in hud.m_statusEffects)
            if (entry) Object.Destroy(entry.gameObject);
        hud.m_statusEffects.Clear();
    }

    private static void Create(Hud hud)
    {
        Dispose();
        _hud = hud;
        GameObject root = new(RootName, typeof(RectTransform));
        _root = (RectTransform)root.transform;
        _root.SetParent(hud.m_statusEffectListRoot, false);
        // Covers the game's list exactly, so the copies sit where the game's own icons would.
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.offsetMin = Vector2.zero;
        _root.offsetMax = Vector2.zero;
        _root.pivot = hud.m_statusEffectListRoot.pivot;
        _metrics = Metrics.Measure(Tile(0), hud);
        Tiles[0].Root.gameObject.SetActive(false);
        _handle = CreateHandle(_metrics.Anchor);
        _backs = CreateBacks();
        _slot = SlotLook.Find(hud);
        Plugin.ModLogger.LogInfo(_slot != null
            ? $"Status Boxes use the hotbar's slot background ({_slot.Sprite.name})."
            : "Hotbar slot background not found: Status Boxes use their own.");
        _mover = root.AddComponent<BoardMover>();
        _mover.Handle = _handle;
    }

    private static RectTransform CreateHandle(Vector2 anchor)
    {
        GameObject created = new("StatusGroups_Handle", typeof(RectTransform));
        RectTransform rect = (RectTransform)created.transform;
        rect.SetParent(_root, false);
        // Behind the icons, in the same units as them.
        rect.SetAsFirstSibling();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = Vector2.zero;
        Image image = created.AddComponent<Image>();
        image.color = HandleColor;
        image.raycastTarget = true;
        Outline outline = created.AddComponent<Outline>();
        outline.effectColor = HandleBorder;
        outline.effectDistance = new Vector2(1f, -1f);
        created.SetActive(false);
        return rect;
    }

    private static TileView Tile(int index)
    {
        while (Tiles.Count <= index) Tiles.Add(TileView.Create(_hud!, _root!));
        return Tiles[index];
    }

    // Right after the backdrop, so the squares and banners stay behind every icon and text made later.
    private static RectTransform CreateBacks()
    {
        GameObject created = new("StatusGroups_Backs", typeof(RectTransform));
        RectTransform rect = (RectTransform)created.transform;
        rect.SetParent(_root, false);
        rect.SetSiblingIndex(_handle!.GetSiblingIndex() + 1);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = _root!.pivot;
        return rect;
    }

    // A status's square, or a group's banner (darker): the hotbar's own slot background when it's found, so the corners
    // are rounded like the item slots', else a painted one.
    private static Image BackAt(int index, bool banner)
    {
        while (Backs.Count <= index)
        {
            GameObject created = new("StatusGroups_Back", typeof(RectTransform));
            created.transform.SetParent(_backs, false);
            Image image = created.AddComponent<Image>();
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = _metrics!.Anchor;
            rect.pivot = Vector2.zero;
            Backs.Add(image);
        }
        Image back = Backs[index];
        Sprite sprite;
        Color color;
        Image.Type type;
        if (_slot != null)
        {
            sprite = _slot.Sprite;
            type = _slot.Type;
            color = banner ? _slot.BannerColor : _slot.Color;
        }
        else
        {
            sprite = banner ? GroupIcons.BannerSprite : GroupIcons.BoxSprite;
            type = Image.Type.Sliced;
            color = Color.white;
        }
        if (back.sprite != sprite) back.sprite = sprite;
        if (back.type != type) back.type = type;
        if (back.color != color) back.color = color;
        if (!back.gameObject.activeSelf) back.gameObject.SetActive(true);
        return back;
    }

    private static void PlaceBack(Image back, Rect rect)
    {
        SetPosition(back.rectTransform, rect.min);
        SetSize(back.rectTransform, rect.size);
        // The game's corners shrink with the square, so a small square looks like a small item slot.
        float density = _slot != null
            ? _slot.Density * _slot.Size / Mathf.Max(1f, Mathf.Min(rect.width, rect.height))
            : GroupIcons.BackDensity;
        if (!Mathf.Approximately(back.pixelsPerUnitMultiplier, density)) back.pixelsPerUnitMultiplier = density;
    }

    // The hotbar's item slot background: its sprite and corner size.
    private sealed class SlotLook
    {
        private static readonly Color SlotTint = new(0f, 0f, 0f, 0.45f);
        private static readonly Color BannerTint = new(0f, 0f, 0f, 0.6f);

        internal Sprite Sprite = null!;
        internal Image.Type Type;
        internal Color Color;
        internal Color BannerColor;
        internal float Density;
        // The slot's size, in screen units.
        internal float Size;

        internal static SlotLook? Find(Hud hud)
        {
            HotkeyBar? bar = hud.GetComponentInChildren<HotkeyBar>(true);
            GameObject? prefab = bar ? bar!.m_elementPrefab : null;
            if (!prefab) return null;
            Image? image = prefab!.GetComponent<Image>();
            if (!image || !image!.sprite)
            {
                image = null;
                foreach (Image child in prefab.GetComponentsInChildren<Image>(true))
                    if (child.sprite && child.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        image = child;
                        break;
                    }
            }
            if (!image) return null;
            Rect rect = image!.rectTransform.rect;
            return new SlotLook
            {
                Sprite = image.sprite,
                Type = image.type == Image.Type.Simple ? Image.Type.Simple : Image.Type.Sliced,
                // The sprite is white, tinted by the game elsewhere: dark see-through grey like the inventory's slots.
                Color = SlotTint,
                BannerColor = BannerTint,
                Density = image.pixelsPerUnitMultiplier,
                // A prefab laid out by its parent may not know its size yet: the hotbar's slots are about this big.
                Size = Mathf.Min(rect.width, rect.height) > 8f ? Mathf.Min(rect.width, rect.height) : 70f
            };
        }
    }

    private static TileView MarkerAt(int index)
    {
        while (Markers.Count <= index) Markers.Add(TileView.Create(_hud!, _root!));
        return Markers[index];
    }

    private static Label LabelAt(int index)
    {
        while (Labels.Count <= index)
            Labels.Add(new Label(
                NewText("StatusGroups_Name", TextWrappingModes.Normal, TextOverflowModes.Overflow),
                NewText("StatusGroups_Time", TextWrappingModes.NoWrap, TextOverflowModes.Overflow)));
        return Labels[index];
    }

    private static Caption CaptionAt(int index, string groupId, Style style, TextAlignmentOptions alignment)
    {
        while (Captions.Count <= index)
            Captions.Add(new Caption(NewText("StatusGroups_Caption", TextWrappingModes.NoWrap, TextOverflowModes.Overflow)));
        Caption caption = Captions[index];
        caption.Set(GroupRegistry.Get(groupId).Name, style.GroupFontSize, style.Color, alignment);
        return caption;
    }

    // A text on the board, in the game's status font, placed in the same units as the icons.
    private static TMP_Text NewText(string name, TextWrappingModes wrapping, TextOverflowModes overflow)
    {
        Metrics metrics = _metrics!;
        GameObject created = new(name, typeof(RectTransform));
        created.transform.SetParent(_root, false);
        TextMeshProUGUI text = created.AddComponent<TextMeshProUGUI>();
        if (metrics.Font)
        {
            text.font = metrics.Font;
            if (metrics.FontMaterial) text.fontSharedMaterial = metrics.FontMaterial;
        }
        text.enableAutoSizing = false;
        text.textWrappingMode = wrapping;
        text.overflowMode = overflow;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = metrics.Anchor;
        rect.pivot = new Vector2(0f, 1f);
        created.SetActive(false);
        return text;
    }

    // A group's name above its block. Measured only when its text or size changes.
    private sealed class Caption(TMP_Text text)
    {
        internal float Width { get; private set; }
        internal float Height { get; private set; }
        private string? _measured;

        internal void Set(string value, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            if (!text.gameObject.activeSelf) text.gameObject.SetActive(true);
            if (SetFontSize(text, fontSize)) _measured = null;
            SetText(text, value, color);
            SetAlignment(text, alignment);
            if (_measured == value) return;
            _measured = value;
            Vector2 size = Measure(text, value);
            Width = size.x;
            Height = size.y;
        }

        // Bottom corner (left or right, by pivotX) at the position.
        internal void Place(Vector2 position, Vector2 size, float pivotX)
        {
            Vector2 pivot = new(pivotX, 0f);
            if (text.rectTransform.pivot != pivot) text.rectTransform.pivot = pivot;
            SetPosition(text.rectTransform, position);
            SetSize(text.rectTransform, size);
        }

        internal void Hide()
        {
            if (text.gameObject.activeSelf) text.gameObject.SetActive(false);
        }
    }

    // One status's name and time, under its icon or on its square. Sizes are measured only when the
    // text, the time's shape or the font size changes, never every second.
    private sealed class Label(TMP_Text name, TMP_Text time)
    {
        // The name on one line, 0 without a name; the room for the time's shape, 0 without a time; a line's height.
        internal float NameWidth { get; private set; }
        internal float TimeWidth { get; private set; }
        internal float TimeHeight { get; private set; }
        private bool _measured;
        private string? _measuredName;
        private string? _lastTime;
        private string? _measuredPattern;
        private float _heightFor = -1f;
        private float _height;
        // Under a minute left: in Status Boxes the time grows, in the square's middle: to this size, or 0 to fill it.
        private float? _endingSoonSize;
        private float _timeFontSize;

        // No name: hidden and takes no room (Show Names off). endingSoonSize: set only with under a minute left.
        internal void Set(string? text, string? timeText, float fontSize, float timeFontSize, Color color, Color timeColor,
            float? endingSoonSize)
        {
            _endingSoonSize = endingSoonSize;
            _timeFontSize = timeFontSize;
            bool resized = SetFontSize(name, fontSize) | SetFontSize(time, timeFontSize);
            if (resized || !_measured)
            {
                _measured = true;
                _measuredName = _measuredPattern = _lastTime = null;
                NameWidth = TimeWidth = 0f;
                _heightFor = -1f;
                TimeHeight = Measure(time, BoardPlan.ShortestTimePattern).y;
            }
            SetText(name, text ?? string.Empty, color);
            SetText(time, timeText ?? string.Empty, timeColor);
            if (_measuredName != text)
            {
                _measuredName = text;
                NameWidth = text == null ? 0f : Measure(name, text).x;
                _heightFor = -1f;
            }
            if (_lastTime != timeText)
            {
                _lastTime = timeText;
                string? pattern = timeText == null ? null : BoardPlan.TimePattern(timeText);
                if (_measuredPattern != pattern)
                {
                    _measuredPattern = pattern;
                    TimeWidth = pattern == null ? 0f : Measure(time, pattern).x;
                }
            }
            if (name.gameObject.activeSelf != (text != null)) name.gameObject.SetActive(text != null);
            if (time.gameObject.activeSelf != (timeText != null)) time.gameObject.SetActive(timeText != null);
        }

        // How tall the name is when it may be this wide, wrapped onto as many lines as it needs.
        internal float Height(float width)
        {
            if (!Mathf.Approximately(_heightFor, width))
            {
                _heightFor = width;
                _height = _measuredName == null ? 0f : Mathf.Ceil(name.GetPreferredValues(name.text, width, 0f).y);
            }
            return _height;
        }

        // Icons and Stacked: the time centred under the icon, the name centred under the time.
        internal void PlaceUnder(float center, float top, float width, float timeHeight, float nameHeight)
        {
            float left = center - width * 0.5f;
            SetScale(time.rectTransform, 1f);
            SetAlignment(time, TextAlignmentOptions.Top);
            SetAlignment(name, TextAlignmentOptions.Top);
            Fit(time.rectTransform, new Vector2(left, top), new Vector2(width, timeHeight));
            Fit(name.rectTransform, new Vector2(left, top - timeHeight), new Vector2(width, nameHeight));
        }

        // Status Boxes: the time on the icon's bottom, inside its square, the name under the square. With under a minute
        // left the time is scaled up (to Ending Soon Font Size, or to fill the square) and centred on it (scaled, not resized, so cells keep their width).
        internal void PlaceOn(float center, float boxBottom, float width, float timeHeight, float nameHeight, float boxWidth,
            float boxHeight)
        {
            float left = center - width * 0.5f;
            SetAlignment(name, TextAlignmentOptions.Top);
            Fit(name.rectTransform, new Vector2(left, boxBottom), new Vector2(width, nameHeight));
            if (_endingSoonSize is { } size && TimeWidth > 0f && TimeHeight > 0f)
            {
                float zoom = size > 0f && _timeFontSize > 0f
                    ? size / _timeFontSize
                    : Mathf.Max(1f, Mathf.Min(boxWidth * 0.95f / TimeWidth, boxHeight * 0.8f / TimeHeight));
                SetAlignment(time, TextAlignmentOptions.Center);
                SetScale(time.rectTransform, zoom);
                // Pivot at the top left: shift so the scaled text's middle is the square's.
                float middle = boxBottom + boxHeight * 0.5f;
                Fit(time.rectTransform, new Vector2(center - TimeWidth * zoom * 0.5f, middle + TimeHeight * zoom * 0.5f),
                    new Vector2(TimeWidth, TimeHeight));
                return;
            }
            // On the square's bottom, shrunk when wider than the square (a status's own text, like "Comfort: 1").
            float shrink = TimeWidth > 0f ? Mathf.Min(1f, boxWidth * BoxTextFill / TimeWidth) : 1f;
            SetScale(time.rectTransform, shrink);
            SetAlignment(time, TextAlignmentOptions.Bottom);
            Fit(time.rectTransform, new Vector2(center - boxWidth * 0.5f, boxBottom + timeHeight * shrink),
                new Vector2(boxWidth / shrink, timeHeight));
        }

        private static void SetScale(RectTransform rect, float scale)
        {
            Vector3 wanted = new(scale, scale, 1f);
            if (rect.localScale != wanted) rect.localScale = wanted;
        }

        internal void Hide()
        {
            if (name.gameObject.activeSelf) name.gameObject.SetActive(false);
            if (time.gameObject.activeSelf) time.gameObject.SetActive(false);
        }

        // Both texts have their pivot at the top left.
        private static void Fit(RectTransform rect, Vector2 position, Vector2 size)
        {
            SetPosition(rect, position);
            SetSize(rect, size);
        }
    }

    // The parts of one copy of the game's status template.
    private sealed class TileView
    {
        // The holder the board moves and scales; the template's copy sits inside it. The game's "new" flash animates the
        // copy's own scale, so without the holder a new icon would pop up at full size, ignoring Icon Size.
        internal RectTransform Root = null!;
        internal Image Icon = null!;
        internal GameObject? Cooldown;
        internal TMP_Text? Name;
        internal TMP_Text? Time;
        internal TMP_Text? Count;
        internal Animator? Animator;

        internal static TileView Create(Hud hud, RectTransform parent)
        {
            RectTransform template = hud.m_statusEffectTemplate;
            GameObject holder = new("StatusGroups_Tile", typeof(RectTransform));
            RectTransform holderRect = (RectTransform)holder.transform;
            holderRect.SetParent(parent, false);
            // Same anchors, pivot and size as the template, so its units are the template's own.
            holderRect.anchorMin = template.anchorMin;
            holderRect.anchorMax = template.anchorMax;
            holderRect.pivot = template.pivot;
            holderRect.sizeDelta = template.sizeDelta;
            RectTransform root = Object.Instantiate(template, holderRect);
            root.gameObject.name = "StatusGroups_Status";
            root.anchorMin = root.anchorMax = template.pivot;
            root.anchoredPosition = Vector2.zero;
            root.localScale = Vector3.one;
            root.gameObject.SetActive(true);
            TileView tile = new()
            {
                Root = holderRect,
                Icon = root.Find("Icon").GetComponent<Image>(),
                Cooldown = root.Find("Cooldown") is { } cooldown ? cooldown.gameObject : null,
                Time = root.Find("TimeText") is { } time ? time.GetComponent<TMP_Text>() : null,
                Animator = root.GetComponentInChildren<Animator>(true)
            };
            // A sprite that isn't square keeps its shape inside the icon instead of being stretched to fill it.
            tile.Icon.preserveAspect = true;
            tile.Name = root.Find("Name") is { } name && name.GetComponent<TMP_Text>() is { } named ? named : FirstTextBut(root, tile.Time);
            tile.Count = CountBadge(tile);
            return tile;
        }

        private static TMP_Text? FirstTextBut(Transform root, TMP_Text? not)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text != not) return text;
            return null;
        }

        // How many statuses a stacked group holds, in the icon's top right corner, in the time's font.
        private static TMP_Text? CountBadge(TileView tile)
        {
            TMP_Text? source = tile.Time ? tile.Time : tile.Name;
            if (!source) return null;
            TMP_Text count = Object.Instantiate(source!, tile.Icon.transform);
            count.gameObject.name = "StatusGroups_Count";
            foreach (Transform child in count.transform) Object.Destroy(child.gameObject);
            RectTransform rect = count.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(4f, 4f);
            rect.sizeDelta = new Vector2(32f, 20f);
            rect.localScale = Vector3.one;
            count.enableAutoSizing = false;
            count.alignment = TextAlignmentOptions.TopRight;
            count.textWrappingMode = TextWrappingModes.NoWrap;
            count.overflowMode = TextOverflowModes.Overflow;
            count.raycastTarget = false;
            count.text = string.Empty;
            count.gameObject.SetActive(false);
            return count;
        }
    }

    // The template's shape, measured once per HUD in the template's own units (before scaling).
    private sealed class Metrics
    {
        // The icon, from the copy's pivot.
        internal Rect Icon;
        internal Vector2 Anchor;
        internal TMP_FontAsset? Font;
        internal Material? FontMaterial;

        private static readonly Vector3[] Corners = new Vector3[4];

        internal static Metrics Measure(TileView tile, Hud hud)
        {
            RectTransform root = tile.Root;
            root.localScale = Vector3.one;
            // The food timers' font, like the other times on the HUD; else the status template's.
            TMP_Text? font = null;
            if (hud.m_foodTime != null)
                foreach (TMP_Text text in hud.m_foodTime)
                    if (text && text.font)
                    {
                        font = text;
                        break;
                    }
            if (!font) font = tile.Name ? tile.Name : tile.Time;
            return new Metrics
            {
                Icon = Local(root, tile.Icon.rectTransform),
                Anchor = root.anchorMin,
                Font = font ? font!.font : null,
                FontMaterial = font ? font!.fontSharedMaterial : null
            };
        }

        private static Rect Local(RectTransform root, RectTransform part)
        {
            part.GetWorldCorners(Corners);
            Vector2 min = root.InverseTransformPoint(Corners[0]);
            Vector2 max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 point = root.InverseTransformPoint(Corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
