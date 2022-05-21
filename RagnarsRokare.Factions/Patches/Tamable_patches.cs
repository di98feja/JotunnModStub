using HarmonyLib;
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
                if (standing >= Misc.Constants.Standing_Friendly)
                {
                    str += Localization.instance.Localize("\n[<color=yellow><b>$KEY_Use</b></color>] to command");
                }
                if (standing >= Misc.Constants.Standing_Minimum)
                {
                    str += Localization.instance.Localize("\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] to interact");
                    str += $"\nMotivation:{(MobAI.MobManager.AliveMobs[npcZdo.GetString(Constants.Z_UniqueId)] as NpcAI)?.MotivationLevel}, Standing:{standing}";
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
                    var playerFactionId = FactionManager.GetPlayerFaction((user as Player));
                    var standing = StandingsManager.GetStandingTowards(npcZdo, playerFactionId);
                    if (standing < Misc.Constants.Standing_MinimumInteraction)
                    {
                        var npcAi = MobAI.MobManager.AliveMobs[npcZdo.GetString(Constants.Z_UniqueId)] as NpcAI;
                        if (npcAi != null)
                        {
                            string stringToShow = "";
                            if (npcAi.MotivationLevel < Misc.Constants.Motivation_Hopeless)
                            {
                                stringToShow = "*barely aknowledge you*";
                            }
                            else if (npcAi.MotivationLevel < Misc.Constants.Motivation_Hopefull)
                            {
                                npcAi.StartEmote(EmoteManager.Emotes.Nonono);
                                stringToShow = "Who are you, go away!";
                            }
                            else
                            {
                                npcAi.StartEmote(EmoteManager.Emotes.Challenge);
                                stringToShow = "Stand back stranger, I don't trust you";
                            }
                            __instance.GetComponent<Talker>().Say(Talker.Type.Normal, stringToShow);
                        }
                        __result = false;
                        return false;
                    }

                    if (alt)
                    {
                        // Show interaction dialog
                        InteractionManager.StartNpcInteraction(___m_character);
                        __result = false;
                        return false;
                    }

                    if (Time.time - ___m_lastPetTime > 1f)
                    {
                        ___m_lastPetTime = Time.time;
                        if (__instance.m_commandable && FactionManager.IsSameFaction(npcZdo, Player.m_localPlayer))
                        {
                            __instance.Command(user);
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
