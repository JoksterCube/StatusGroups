namespace JoksterCube.StatusGroups.Domain;

// How the HUD shows status effects. Every setting but the layout itself works the same in Icons and Stacked.
internal enum StatusLayout
{
    // Small icons in rows, the time (and name) under each.
    Icons = 0,
    // Like Icons, but each group is one icon unless its Display says otherwise.
    Stacked = 1,
    // The game's own list, untouched.
    Vanilla = 3
}

// How one group shows, whatever the layout.
internal enum GroupDisplay
{
    // As the layout shows groups: stacked in the Stacked layout, expanded otherwise.
    Default = 0,
    // Every status on its own.
    Expanded = 1,
    // One icon for the whole group.
    Stacked = 2,
    // Not shown.
    Hidden = 3
}
