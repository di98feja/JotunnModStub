using RagnarsRokare.MobAI;
using System.Linq;

namespace RagnarsRokare.Factions
{
    internal class Bed_patch
    {
        internal static void Bed_Awake(On.Bed.orig_Awake orig, Bed self)
        {
            orig(self);
            if (self.name.StartsWith("goblin_bed"))
            {
                self.m_nview.GetZDO().m_persistent = true;
                if (self.GetOwner() == 0L)
                {
                    var npc = NpcManager.CreateRandomizedNpc(self.transform.position);
                    var npcZdo = npc.GetComponent<ZNetView>().GetZDO();
                    self.SetOwner(npcZdo.m_uid.id, npc.GetComponent<Tameable>().GetHoverName());
                    npcZdo.Set(Misc.Constants.Z_NpcBedOwnerId, self.m_nview.GetZDO().m_uid);
                    npcZdo.Set(Constants.Z_SavedHomePosition, self.transform.position);
                    npc.SetActive(false);
                }
            }
        }

        internal static string Bed_GetHoverText(On.Bed.orig_GetHoverText orig, Bed self)
        {
            if (!self.name.StartsWith("npc_bed")) return orig(self);

            if (string.IsNullOrEmpty(self.GetOwnerName()))
            {
                return Localization.instance.Localize("Unoccupied bed\nHave a hird member on follow and press [<color=yellow><b>$KEY_Use</b></color>] to assign");
            }
            return orig(self);
        }

        internal static bool Bed_Interact(On.Bed.orig_Interact orig, Bed self, Humanoid human, bool repeat, bool alt)
        {
            if (self.name.StartsWith("npc_bed") && string.IsNullOrEmpty(self.GetOwnerName()))
            {
                var npc = MobManager.AliveMobs.Where(m => m.Value.HasInstance()).Where(m => (m.Value.Instance as MonsterAI).GetFollowTarget() == Player.m_localPlayer.gameObject).Select(m => m.Value).FirstOrDefault();
                if (npc != null)
                {
                    var npcZdo = npc.NView.GetZDO();
                    self.SetOwner(npcZdo.m_uid.id, npc.Instance.GetComponent<Tameable>().GetHoverName());
                    npcZdo.Set(Misc.Constants.Z_NpcBedOwnerId, self.m_nview.GetZDO().m_uid);
                    npcZdo.Set(Constants.Z_SavedHomePosition, self.transform.position);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return orig(self, human, repeat, alt);
        }


    }
}
