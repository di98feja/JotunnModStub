using HarmonyLib;
using RagnarsRokare.MobAI;
using System;
using System.Collections.Generic;

namespace RagnarsRokare.Factions
{
    internal class Character_patches
    {
        [HarmonyPatch(typeof(Character), "Awake")]
        static class Character_Awake_Patch
        {
            static void Postfix(Character __instance, ref ZNetView ___m_nview)
            {
                if (!__instance.name.Contains("NPC")) return;

                var ai = __instance.GetBaseAI() as MonsterAI;
                var mobInfo = new WorkerAIConfig
                {
                    Agressiveness = UnityEngine.Random.Range(1, 10),
                    Awareness = UnityEngine.Random.Range(1, 10),
                    Intelligence = UnityEngine.Random.Range(1, 10),
                    Mobility = UnityEngine.Random.Range(1, 10),
                };
                Tameable tameable = Helpers.GetOrAddTameable(__instance.gameObject);
                tameable.m_commandable = true;
                string uniqueId = Helpers.GetOrCreateUniqueId(___m_nview);

                try
                {
                    MobManager.RegisterMob(__instance, uniqueId, "Worker", mobInfo);
                }
                catch (ArgumentException e)
                {
                    Jotunn.Logger.LogError($"Failed to register Mob AI. {e.Message}");
                    return;
                }
                __instance.m_faction = Character.Faction.Players;
                ai.m_consumeItems.Clear();
                ai.m_consumeItems.AddRange(CreateDropItemList(new string[] { "Raspberry,Honey,Blueberries" }));
                ai.m_consumeSearchRange = mobInfo.Awareness * 5;
                ai.m_randomMoveRange = mobInfo.Mobility * 2;
                ai.m_randomMoveInterval = 15 - mobInfo.Mobility;
                string givenName = ___m_nview?.GetZDO()?.GetString(Constants.Z_GivenName);
                Jotunn.Logger.LogInfo($"{__instance.m_name} woke up");
            }
        }
        public static IEnumerable<ItemDrop> CreateDropItemList(IEnumerable<string> itemNames)
        {
            foreach (var itemName in itemNames)
            {
                var item = ObjectDB.instance.GetItemByName(itemName);
                if (null == item)
                {
                    Jotunn.Logger.LogWarning($"Cannot find item {itemName} in objectDB");
                    continue;
                }
                yield return item;
            }
        }

    }
}
