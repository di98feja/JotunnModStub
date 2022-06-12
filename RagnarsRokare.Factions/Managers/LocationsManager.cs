using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    [HarmonyPatch()]
    internal static class LocationsManager
    {
        private static GameObject m_npcBedPrefab;

        public static void LoadAssets()
        {
            var embeddedResourceBundle = AssetUtils.LoadAssetBundleFromResources("npcbed", typeof(Factions).Assembly);
            var npcBedPrefab = embeddedResourceBundle.LoadAsset<GameObject>("Assets/NpcBed/npc_bed.prefab");
            //PrefabManager.Instance.AddPrefab(new Jotunn.Entities.CustomPrefab(npcBedPrefab, true));
            Jotunn.Logger.LogInfo($"Embedded resources: {string.Join(",", typeof(Factions).Assembly.GetManifestResourceNames())}");

            PieceManager.Instance.AddPiece(new Jotunn.Entities.CustomPiece(npcBedPrefab, true, 
                new Jotunn.Configs.PieceConfig 
                { 
                    Name = "Npc bed", 
                    PieceTable = "Hammer",
                    Category = "Hird",
                    Description = "A simple bed for your hird"
                }));
            //m_npcBedPrefab = PrefabManager.Instance.GetPrefab("npc_bed");

            var goblinBedPrefab = PrefabManager.Instance.GetPrefab("goblin_bed");
            goblinBedPrefab.gameObject.AddComponent<Bed>();
            var collider = goblinBedPrefab.GetComponent<Collider>() as BoxCollider;
            collider.size = new Vector3(1.2f, 0.2f, 2.8f);

            embeddedResourceBundle.Unload(false);
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.PrepareNetViews)), HarmonyPrefix, HarmonyPriority(Priority.First)]
        internal static void PrepareNetViews(ZoneSystem __instance, GameObject root, List<ZNetView> views)
        {
            string[] npcLocations = new string[] { "WoodHouse1", "WoodHouse2", "Farm" };
            if (npcLocations.Contains(root.name))
            {
                RandomSpawn[] componentsInChildren = root.GetComponentsInChildren<RandomSpawn>(includeInactive: true);
                var gbs = componentsInChildren.Where(c => c.name.StartsWith("goblin_bed"));
                foreach (var goblinBed in gbs)
                {
                    goblinBed.m_chanceToSpawn = 100f;
                }
                var bh = componentsInChildren.FirstOrDefault(r => r.name == "Beehive");
                if (bh)
                {
                    bh.m_chanceToSpawn = 0f;
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
