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
            LoadAssets();
            InitInputs();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);

            MobAI.MobManager.RegisterMobAI(typeof(NpcAI));
            ZoneManager.OnVanillaLocationsAvailable += LocationsManager.SetupNpcLocations;

            // Jotunn comes with MonoMod Detours enabled for hooking Valheim's code
            // https://github.com/MonoMod/MonoMod
            On.FejdStartup.Awake += FejdStartup_Awake;
            On.ZRoutedRpc.ctor += InitRPCs;
            On.Bed.Awake += Bed_Awake;
            On.Player.OnSpawned += Player_OnSpawned;
            // Jotunn comes with its own Logger class to provide a consistent Log style for all mods using it
            Jotunn.Logger.LogInfo($"{PluginName} v{PluginVersion} has landed");

            // To learn more about Jotunn's features, go to
            // https://valheim-modding.github.io/Jotunn/tutorials/overview.html
        }

        private void Player_OnSpawned(On.Player.orig_OnSpawned orig, Player self)
        {
            orig(self);
            foreach (var loc in ZoneSystem.instance.m_locationInstances)
            {
                if (loc.Value.m_location.m_prefabName.Contains("WoodHouse") && loc.Value.m_location.m_netViews.Any(z => z.gameObject.name.Contains("goblin_bed")))
                {
                    Minimap.instance.AddPin(loc.Value.m_position, Minimap.PinType.Bed, loc.Value.m_location.m_prefabName, !(loc.Value.m_location.m_netViews.FirstOrDefault(n => n.gameObject.GetComponent<Bed>()) ?? false), false);
                }
            }
        }

        private void Bed_Awake(On.Bed.orig_Awake orig, Bed self)
        {
            orig(self);
            if (self.name.StartsWith("goblin_bed"))
            {
                if (self.GetOwner() == 0L)
                {
                    var npc = NpcManager.CreateRandomizedNpc(self.transform.position);
                    npc.SetActive(false);
                    var npcZdo = npc.GetComponent<ZNetView>().GetZDO();
                    self.SetOwner(npcZdo.m_uid.id, npc.GetComponent<Tameable>().GetHoverName());
                }
            }
        }

        private void FejdStartup_Awake(On.FejdStartup.orig_Awake orig, FejdStartup self)
        {
            // This code runs before Valheim's FejdStartup.Awake
            Jotunn.Logger.LogInfo("FejdStartup is going to awake");

            // Call this method so the original game method is invoked
            orig(self);

            // This code runs after Valheim's FejdStartup.Awake
            Jotunn.Logger.LogInfo("FejdStartup has awoken");
        }

        private void InitRPCs(On.ZRoutedRpc.orig_ctor orig, global::ZRoutedRpc self, bool server)
        {
            orig(self, server);
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
                NpcManager.CreateRandomizedNpc(Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward);
            }
        }
    }
}