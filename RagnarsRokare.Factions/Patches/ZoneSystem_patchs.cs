using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal class ZoneSystem_patches
    {
        [HarmonyPatch(typeof(ZoneSystem), "SpawnLocation")]
        static class ZoneSystem_SpawnLocation_Patch
        {
            static void Postfix(ZoneSystem.ZoneLocation location, int seed, Vector3 pos, Quaternion rot, ZoneSystem.SpawnMode mode)
            {
                foreach (var bed in location.m_netViews.Where(n => n.gameObject.name.Contains("goblin_bed")))
                {
                    bed.gameObject.AddComponent<Bed>();
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
}
