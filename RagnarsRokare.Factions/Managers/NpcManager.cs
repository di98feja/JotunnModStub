using Jotunn;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal static class NpcManager
    {
        //private static GameObject m_npcPrefab;
        private static readonly System.Random m_random = new System.Random();
        private static List<ZDOID> m_allNpcZDOIDs = new List<ZDOID>();

        public static void InitRPCs()
        {
            //ZRoutedRpc.instance.Register<ZPackage>(Constants.Z_RegisteredMobsChangedEvent, RegisteredMobsChangedEvent_RPC);

        }

        internal static void RegisteredMobsChangedEvent_RPC(long sender, ZPackage pkg)
        {
            Debug.Log("Got RegisteredMobsChangedEvent to NpcManager");
            m_allNpcZDOIDs.Clear();
            bool endOfStream = false;

            while (!endOfStream)
            {
                try
                {
                    m_allNpcZDOIDs.Add(pkg.ReadZDOID());
                }
                catch (System.IO.EndOfStreamException)
                {
                    endOfStream = true;
                }
            }
            Debug.Log($"NpcManager now track {m_allNpcZDOIDs.Count} NPCs");
        }

        public static void LoadAssets()
        {
            var embeddedResourceBundle = AssetUtils.LoadAssetBundleFromResources("npc", typeof(Factions).Assembly);
            var npcPrefab = embeddedResourceBundle.LoadAsset<GameObject>("Assets/NPC/NPC.prefab");
            //npcPrefab.FixReferences(true);
            Jotunn.Logger.LogInfo($"Embedded resources: {string.Join(",", typeof(Factions).Assembly.GetManifestResourceNames())}");
            var npcCutstomCreature = new CustomCreature(npcPrefab, true, new Jotunn.Configs.CreatureConfig
            {
                Name = "NPC"
            });
            npcPrefab.AddComponent<NpcContainer>();
            CreatureManager.Instance.AddCreature(npcCutstomCreature);

            //var npcCustomPrefab = new CustomPrefab(npcPrefab, fixReference:true);
            //npcPrefab.name = "NPC";
            //PrefabManager.Instance.AddPrefab(npcCustomPrefab);
            //m_npcPrefab = CreatureManager.Instance.GetCreaturePrefab("NPC");
   //         var human = m_npcPrefab.AddComponent<HumanStub>();
            //var humanoid = m_npcPrefab.GetComponent<Humanoid>();
            

            embeddedResourceBundle.Unload(false);
        }

        public static ZDO[] GetAllActiveNPCs()
        {
            var zdoList = new List<ZDO>();
            foreach (var zid in m_allNpcZDOIDs)
            {
                var zdo = ZDOMan.instance.GetZDO(zid);
                if (!(zdo?.IsValid() ?? false)) continue;
                zdoList.Add(zdo);
            }
            return zdoList.ToArray();
        }

        public static string CreateAndSetRandomNameForNpc(Character npc)
        {
            string newName = IsMale(npc.gameObject) ? RandomizeMaleName() : RandomizeFemaleName();

            var nview = npc.GetComponent<ZNetView>();
            nview.ClaimOwnership();
            nview.GetZDO().Set(Constants.Z_GivenName, newName);
            var tamable = npc.GetComponent<Tameable>();
            tamable.SetText(newName);

            // Update bed text
            var bedZDOId = npc.m_nview.GetZDO().GetZDOID(Misc.Constants.Z_NpcBedOwnerId);
            if (bedZDOId != ZDOID.None)
            {
                var bedGO = ZNetScene.instance.FindInstance(bedZDOId);
                bedGO.GetComponent<Bed>().SetOwner((long)npc.m_nview.GetZDO().m_uid.id, newName);
            }

            return newName;
        }

        private static string RandomizeMaleName()
        {
            string[] beginnings = new string[] { "Sig", "Ulf", "Tor", "Alf", "Sten" };
            string[] endings = new string[] { "var", "gor", "dur", "bar", "e", "beorn" };
            return $"{beginnings[m_random.Next(beginnings.Length)]}{endings[m_random.Next(endings.Length)]}";
        }

        private static string RandomizeFemaleName()
        {
            string[] beginnings = new string[] { "Sig", "Hild", "Gun", "Britt", "Maj"};
            string[] endings = new string[] { "vor", "rid", "lin", "a" };
            return $"{beginnings[m_random.Next(beginnings.Length)]}{endings[m_random.Next(endings.Length)]}";
        }

        public static GameObject CreateRandomizedNpc(Transform parent, Vector3 localPosition)
        {
            var npc = UnityEngine.Object.Instantiate(CreatureManager.Instance.GetCreaturePrefab("NPC"), parent);
            npc.transform.localPosition = localPosition;
            return npc;
        }

        public static GameObject CreateRandomizedNpc(Vector3 position)
        {
            var npc = UnityEngine.Object.Instantiate(CreatureManager.Instance.GetCreaturePrefab("NPC"), position, Quaternion.LookRotation(Vector3.forward));
            //InitNpc(npc);
            return npc;
        }

        public static bool NeedsInit(GameObject npc)
        {
            return !npc.GetComponent<Character>().IsTamed();
        }

        public static void InitNpc(GameObject npc)
        {
            var nview = npc.GetComponent<ZNetView>();
            if (!nview.IsValid()) return;

            bool valid = ZNetScene.instance.IsPrefabZDOValid(nview.GetZDO());

            nview.GetZDO().m_persistent = true;
            nview.GetZDO().Set(Misc.Constants.Z_Faction, FactionManager.DefaultNPCFactionId);
            nview.GetZDO().SetPrefab(nview.GetPrefabName().GetStableHashCode());
            Tameable tameable = Helpers.GetOrAddTameable(npc);
            npc.GetComponent<Character>().SetTamed(tamed: true);
            var name = CreateNpcName();
            tameable.SetText(name);
            npc.GetComponent<Character>().m_name = name;
            int beardNr = m_random.Next(11);
            int hairNr = m_random.Next(15);
            var visEquip = npc.GetComponent<VisEquipment>();
            visEquip.SetModel(m_random.Next(2));
            if (visEquip.GetModelIndex() == 0)
            {
                visEquip.SetBeardItem($"Beard{(beardNr == 0 ? "None" : beardNr.ToString())}");
            }
            visEquip.SetHairItem($"Hair{(hairNr == 0 ? "None" : hairNr.ToString())}");
            var humanoid = npc.GetComponent<Humanoid>();
            humanoid.GiveDefaultItems();
            foreach (var item in humanoid.m_defaultItems)
            {
                humanoid.EquipItem(item.GetComponent<ItemDrop>().m_itemData, false);
            }
            npc.GetComponent<Humanoid>().HideHandItems();
            nview.GetZDO().Set(Constants.Z_trainedAssignments, "Npc");
        }

        public static bool IsMale(GameObject npc)
        {
            var visEquip = npc.GetComponent<VisEquipment>();
            return visEquip.GetModelIndex() == 0;
        }

        private static string CreateNpcName()
        {
            return $"{NpcDescriptions[m_random.Next(NpcDescriptions.Length)]} Norse";
        }

        private static string[] NpcDescriptions = new string[] { "Miserable", "Unwashed", "Starving", "Sickly", "Apathic", "Confused" };
    }
}
