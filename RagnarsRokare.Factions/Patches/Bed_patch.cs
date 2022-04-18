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
    }
}
