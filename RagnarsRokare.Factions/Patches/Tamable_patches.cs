using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RagnarsRokare.Factions.Patches
{
    internal class Tamable_patches
    {
        [HarmonyPatch(typeof(Tameable), "GetHoverText")]
        static class Tameable_GetHoverName_Patch
        {
            static bool Prefix(Tameable __instance, ref string __result, ZNetView ___m_nview, Character ___m_character)
            {
                if (!__instance.name.Contains("NPC")) return true;
                if (!___m_character.IsTamed()) return true;
                if (!___m_nview.IsValid())
                {
                    __result = string.Empty;
                    return true;
                }
                string aiStatus = ___m_nview.GetZDO().GetString(Constants.Z_AiStatus) ?? Traverse.Create(__instance).Method("GetStatusString").GetValue() as string;
                string str = Localization.instance.Localize(___m_character.GetHoverName());
                str += Localization.instance.Localize($" ({aiStatus})");
                var npcZdo = ___m_nview.GetZDO();
                var playerFactionId = Player.m_localPlayer.m_nview.GetZDO().GetString(Misc.Constants.Z_Faction);
                var standing = StandingsManager.GetStandingTowards(npcZdo, playerFactionId);
                if (standing >= Misc.Constants.FriendlyStanding)
                {
                    str += Localization.instance.Localize("\n[<color=yellow><b>$KEY_Use</b></color>] to command");
                }
                if (standing >= Misc.Constants.KnownStanding)
                {
                    str += Localization.instance.Localize("\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] to interact");
                }
                __result = str;
                return false;
            }
        }

        [HarmonyPatch(typeof(Tameable), "Interact")]
        static class Tameable_Interact_Patch
        {
            static bool Prefix(Tameable __instance, ref bool __result, Humanoid user, bool hold, ZNetView ___m_nview, ref Character ___m_character,
                ref float ___m_lastPetTime, bool alt)
            {
                if (!__instance.name.Contains("NPC")) return true;

                if (!___m_nview.IsValid())
                {
                    __result = false;
                    return true;
                }
                string hoverName = ___m_character.GetHoverName();
                if (___m_character.IsTamed())
                {
                    var npcZdo = ___m_nview.GetZDO();
                    var playerFactionId = user.m_nview.GetZDO().GetString(Misc.Constants.Z_Faction);
                    var standing = StandingsManager.GetStandingTowards(npcZdo, playerFactionId);
                    if (standing <= Misc.Constants.KnownStanding)
                    {
                        var npcAi = MobAI.MobManager.AliveMobs[npcZdo.GetString(Constants.Z_UniqueId)] as NpcAI;
                        if (npcAi != null)
                        {
                            npcAi.StartEmote(EmoteManager.Emotes.Nonono);
                        }
                        __instance.GetComponent<Talker>().Say(Talker.Type.Normal, "Who are you? Go away!");
                        __result = false;
                        return false;
                    }

                    if (alt && standing >= Misc.Constants.KnownStanding)
                    {
                        // Show interaction dialog
                        __result = false;
                        return false;
                    }

                    if (Time.time - ___m_lastPetTime > 1f)
                    {
                        ___m_lastPetTime = Time.time;
                        if (__instance.m_commandable && standing >= Misc.Constants.FriendlyStanding)
                        {
                            __instance.Command( user );
                        }
                        __result = true;
                        return false;
                    }
                    __result = false;
                    return false;
                }
                __result = false;
                return false;
            }
        }
    }
}
