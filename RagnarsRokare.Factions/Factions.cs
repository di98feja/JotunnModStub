// RagnarsRokare.Factions
// a Valheim mod 
// 
// File:    Factions.cs
// Project: RagnarsRokare.Factions

using BepInEx;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(MobAI.MobAILib.ModId)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class Factions : BaseUnityPlugin
    {
        public const string PluginGUID = "025F5D7C-8046-4A18-9539-58289CA229EA";
        public const string PluginName = "RagnarsRokare.Factions";
        public const string PluginVersion = "0.0.1";

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private void Awake()
        {
            InitInputs();
            var harmony = new Harmony(PluginGUID);
            harmony.PatchAll(typeof(Factions));
            harmony.PatchAll(typeof(Bed_patch));
            harmony.PatchAll(typeof(LocalizationManager));
            harmony.PatchAll(typeof(AttachManager));
            harmony.PatchAll(typeof(NpcContainer));
            harmony.PatchAll(typeof(Character_patches));
            harmony.PatchAll(typeof(Minimap_patches));
            harmony.PatchAll(typeof(Sign_patches));
            harmony.PatchAll(typeof(Tamable_patches));

            PrefabManager.OnVanillaPrefabsAvailable += PrefabManager_OnVanillaPrefabsAvailable;
            
            MobAI.MobManager.RegisterMobAI(typeof(NpcAI));

            // Jotunn comes with its own Logger class to provide a consistent Log style for all mods using it
            Jotunn.Logger.LogInfo($"{PluginName} v{PluginVersion} has landed");

            // To learn more about Jotunn's features, go to
            // https://valheim-modding.github.io/Jotunn/tutorials/overview.html
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GiveDefaultItems)), HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static bool Humanoid_GiveDefaultItems(Humanoid __instance)
        {
            if (__instance.gameObject.name.Contains("NPC"))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void PrefabManager_OnVanillaPrefabsAvailable()
        {
            LoadAssets();
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake)), HarmonyPostfix, HarmonyPriority(Priority.First)]
        private static void ObjectDB_Awake()
        {
            ErrandsManager.Init();
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned)), HarmonyPostfix]
        private static void Player_OnSpawned(Player __instance)
        {
            foreach (var loc in ZoneSystem.instance.m_locationInstances)
            {
                if (loc.Value.m_location.m_prefabName.Contains("WoodHouse") && loc.Value.m_location.m_netViews.Any(z => z.gameObject.name.Contains("goblin_bed")))
                {
                    Minimap.instance.AddPin(loc.Value.m_position, Minimap.PinType.Bed, loc.Value.m_location.m_prefabName, !(loc.Value.m_location.m_netViews.FirstOrDefault(n => n.gameObject.GetComponent<Bed>()) ?? false), false);
                }
                //if (loc.Value.m_location.m_netViews.Any(z => z.gameObject.name.Contains("Beehive")))
                //{
                //    Minimap.instance.AddPin(loc.Value.m_position, Minimap.PinType.Icon0, loc.Value.m_location.m_prefabName, !(loc.Value.m_location.m_netViews.FirstOrDefault(n => n.gameObject.GetComponent<Bed>()) ?? false), false);
                //}
            }
        }

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake)), HarmonyPostfix, HarmonyPriority(Priority.First)]
        private void FejdStartup_Awake(FejdStartup __instance)
        {
            AttachManager.Init();
            MobAI.EventManager.RegisteredMobsChanged += Minimap_patches.RegisteredMobsChanged;
            MobAI.EventManager.ServerShutdown += NpcManager.SaveAllNPCs;
        }

        [HarmonyPatch(typeof(ZRoutedRpc), MethodType.Constructor), HarmonyPostfix, HarmonyPriority(Priority.First)]
        private void InitRPCs(ZRoutedRpc __instance, bool server)
        {
            NpcManager.InitRPCs();
        }

        private void LoadAssets()
        {
            // Load asset bundle from embedded resources
            NpcManager.LoadAssets();
            LocationsManager.LoadAssets();
        }

        private void InitInputs()
        {
            SpawnNpcKey = new Jotunn.Configs.ButtonConfig
            {
                Key = KeyCode.Delete,
                Name = "SpawnNpc",
                ActiveInCustomGUI = true,
                ActiveInGUI = true,
            };
            InputManager.Instance.AddButton(PluginGUID, SpawnNpcKey);
        }

        private Jotunn.Configs.ButtonConfig SpawnNpcKey;

        private void Update()
        {
            if (ZInput.m_instance == null) return;
            if (ZInput.GetButtonUp(SpawnNpcKey.Name))
            {
                var npc = NpcManager.CreateRandomizedNpc(Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward);
                FactionManager.SetFaction(npc.GetComponent<ZNetView>().GetZDO(), FactionManager.GetLocalPlayerFaction().FactionId);
                StandingsManager.SetStandingTowards(npc.GetComponent<ZNetView>().GetZDO(), FactionManager.GetLocalPlayerFaction(), Misc.Constants.Standing_MinimumInteraction);
            }
        }
    }
}