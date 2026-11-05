using System.Collections.Generic;
using HarmonyLib;
using JoksterCube.StatusGroups.Domain;

namespace JoksterCube.StatusGroups.Patches;

internal static class HudPatches
{
    // Every frame, with the local player's statuses that have an icon. The chosen layout draws them instead of the game,
    // except for Vanilla (or when the layout broke after a game update: see PatchGuard).
    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateStatusEffects))]
    private static class UpdateStatusEffectsPatch
    {
        [HarmonyPriority(Priority.Low)]
        private static bool Prefix(Hud __instance, List<StatusEffect> statusEffects) => StatusBoard.Draw(__instance, statusEffects);
    }

    // The board goes with the HUD on logout; forget it so the next HUD gets its own.
    [HarmonyPatch(typeof(Hud), nameof(Hud.OnDestroy))]
    private static class OnDestroyPatch
    {
        private static void Prefix() => StatusBoard.Dispose();
    }
}
