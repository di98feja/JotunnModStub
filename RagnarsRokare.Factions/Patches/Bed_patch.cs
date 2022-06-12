using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Linq;

namespace RagnarsRokare.Factions
{
    [HarmonyPatch()]
    internal class Bed_patch
    {

        [HarmonyPatch(typeof(Bed), nameof(Bed.Awake)), HarmonyPostfix, HarmonyPriority(Priority.First)]
        internal static void Bed_Awake(Bed __instance)
        {
            if (__instance.name.StartsWith("goblin_bed"))
            {
                __instance.m_nview.GetZDO().m_persistent = true;
                if (__instance.GetOwner() == 0L)
                {
                    __instance.m_nview.ClaimOwnership();
                    if (__instance.m_nview.IsOwner())
                    {
                        var npc = NpcManager.CreateRandomizedNpc(__instance.transform.position);
                        var npcZdo = npc.GetComponent<ZNetView>().GetZDO();
                        __instance.SetOwner(npcZdo.m_uid.id, npc.GetComponent<Tameable>().GetHoverName());
                        npcZdo.Set(Misc.Constants.Z_NpcBedOwnerId, __instance.m_nview.GetZDO().m_uid);
                        npcZdo.Set(Constants.Z_SavedHomePosition, __instance.transform.position);
                        npc.SetActive(false);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Bed), nameof(Bed.GetHoverText)), HarmonyPrefix]
        internal static bool Bed_GetHoverText(Bed __instance, string __result)
        {
            if (!__instance.name.StartsWith("npc_bed")) return true;

            if (string.IsNullOrEmpty(__instance.GetOwnerName()))
            {
                __result = Localization.instance.Localize("Unoccupied bed\nHave a hird member on follow and press [<color=yellow><b>$KEY_Use</b></color>] to assign");
            }
            return false;
        }

        [HarmonyPatch(typeof(Bed), nameof(Bed.Interact)), HarmonyPrefix]
        internal static bool Bed_Interact(Bed __instance, Humanoid human, bool repeat, bool alt, bool __result)
        {
            if (__instance.name.StartsWith("npc_bed") && string.IsNullOrEmpty(__instance.GetOwnerName()))
            {
                var npc = MobManager.AliveMobs.Where(m => m.Value.HasInstance()).Where(m => (m.Value.Instance as MonsterAI).GetFollowTarget() == Player.m_localPlayer.gameObject).Select(m => m.Value).FirstOrDefault();
                if (npc != null)
                {
                    var npcZdo = npc.NView.GetZDO();
                    __instance.SetOwner(npcZdo.m_uid.id, npc.Instance.GetComponent<Tameable>().GetHoverName());
                    npcZdo.Set(Misc.Constants.Z_NpcBedOwnerId, __instance.m_nview.GetZDO().m_uid);
                    npcZdo.Set(Constants.Z_SavedHomePosition, __instance.transform.position);
                    __result = true;
                    return false;
                }
                else
                {
                    __result = false;
                    return false;
                }
            }
            return true;
        }


    }
}
