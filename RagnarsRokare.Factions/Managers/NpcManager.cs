using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal class NpcManager
    {
        private static Jotunn.Configs.CreatureConfig NpcConfig { get; set; }
        private static readonly System.Random m_random = new System.Random();

        public static void LoadAssets()
        {
            var embeddedResourceBundle = AssetUtils.LoadAssetBundleFromResources("npc", typeof(Factions).Assembly);
            var npcPrefab = embeddedResourceBundle.LoadAsset<GameObject>("Assets/PrefabInstance/NPC.prefab");
            Jotunn.Logger.LogInfo($"Embedded resources: {string.Join(",", typeof(Factions).Assembly.GetManifestResourceNames())}");

            NpcConfig = new Jotunn.Configs.CreatureConfig
            {
                Name = "NPC",
                SpawnConfigs = new[]
                {
                    new Jotunn.Configs.SpawnConfig { WorldSpawnEnabled = false }
                }
            };
            CreatureManager.Instance.AddCreature(new CustomCreature(npcPrefab.gameObject, true, NpcConfig));
            embeddedResourceBundle.Unload(false);
        }

        public static GameObject CreateRandomizedNpc(Vector3 position)
        {
            var prefab = CreatureManager.Instance.GetCreaturePrefab(NpcConfig.Name);

            var npc = UnityEngine.Object.Instantiate(prefab, position, Quaternion.LookRotation(Vector3.forward));
            var nview = npc.GetComponent<ZNetView>();
            nview.GetZDO().m_persistent = true;
            Tameable tameable = Helpers.GetOrAddTameable(npc);
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
            return npc;
        }

        private static string CreateNpcName()
        {
            return $"{NpcDescriptions[m_random.Next(NpcDescriptions.Length)]} Norse";
        }

        private static string[] NpcDescriptions = new string[] { "Miserable", "Unwashed", "Starving", "Sickly", "Apathic", "Confused" };
    }
}
