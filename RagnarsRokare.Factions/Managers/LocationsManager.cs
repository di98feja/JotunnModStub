using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal static class LocationsManager
    {
        private static GameObject m_npcBedPrefab;

        public static void LoadAssets()
        {
            var embeddedResourceBundle = AssetUtils.LoadAssetBundleFromResources("npcbed", typeof(Factions).Assembly);
            var npcBedPrefab = embeddedResourceBundle.LoadAsset<GameObject>("Assets/NpcBed/npc_bed.prefab");
            PrefabManager.Instance.AddPrefab(new Jotunn.Entities.CustomPrefab(npcBedPrefab, true));
            m_npcBedPrefab = PrefabManager.Instance.GetPrefab("npc_bed");
            Jotunn.Logger.LogInfo($"Embedded resources: {string.Join(",", typeof(Factions).Assembly.GetManifestResourceNames())}");

            embeddedResourceBundle.Unload(false);
        }

        public static void SetupNpcLocations()
        {
            var goblinBedPrefab = PrefabManager.Instance.GetPrefab("goblin_bed");
            goblinBedPrefab.gameObject.AddComponent<Bed>();

            string[] npcLocations = new string[] { "WoodHouse1", "WoodHouse2", "WoodHouse5", "WoodHouse6", "WoodHouse7", "WoodHouse9", "WoodHouse10", "WoodHouse11", "WoodHouse13" };
            foreach (var npcLocation in npcLocations)
            {
                var loc = ZoneManager.Instance.GetZoneLocation(npcLocation);

                var gb = loc.m_randomSpawns.Find(r => r.name == "goblin_bed");
                if (gb)
                {
                    gb.m_chanceToSpawn = 100f;
                    var bh = loc.m_randomSpawns.Find(r => r.name == "beehive");
                    if (bh)
                    {
                        bh.m_chanceToSpawn = 0f;
                    }
                }
            }
        }

        private static void GetRecursiveGameObject(GameObject go, string name, List<GameObject> found)
        {
            if (go.name.StartsWith(name))
            {
                found.Add(go);
            }

            for (int i = 0; i < go.transform.childCount; i++)
            {
                GetRecursiveGameObject(go.transform.GetChild(i).gameObject, name, found);
            }
        }
    }
}
