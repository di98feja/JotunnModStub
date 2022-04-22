﻿using HarmonyLib;
using Jotunn;
using System.Collections.Generic;
using System.Linq;

namespace RagnarsRokare.Factions.Patches
{
    internal class Minimap_patches
    {
        private static List<ZDOID> m_allMobZDOIDs = new List<ZDOID>();

        internal static void RegisteredMobsChangedEvent_RPC(long sender, ZPackage pkg)
        {
            Logger.LogDebug("Got RegisteredMobsChangedEvent to Minimap_patch");
            m_allMobZDOIDs.Clear();
            bool endOfStream = false;

            while (!endOfStream)
            {
                try
                {
                    m_allMobZDOIDs.Add(pkg.ReadZDOID());
                }
                catch (System.IO.EndOfStreamException)
                {
                    endOfStream = true;
                }
            }
            Logger.LogDebug($"Minimap now track {m_allMobZDOIDs.Count} mobs");
        }

        [HarmonyPatch(typeof(ZoneSystem), "Start")]
        static class ZoneSystem_Start_Patch
        {
            static void Postfix()
            {
                ZRoutedRpc.instance.Register<ZPackage>(Constants.Z_RegisteredMobsChangedEvent, RegisteredMobsChangedEvent_RPC);
            }
        }

        [HarmonyPatch(typeof(Minimap), "UpdateDynamicPins")]
        static class Minimap_UpdateDynamicPins_Patch
        {
            private static readonly Dictionary<ZDOID, Minimap.PinData> m_mobPins = new Dictionary<ZDOID, Minimap.PinData>();
            public static void Postfix()
            {
                try
                {
                    foreach (var zid in m_allMobZDOIDs)
                    {
                        var zdo = ZDOMan.instance.GetZDO(zid);
                        if (!(zdo?.IsValid() ?? false)) continue;
                        if (!FactionManager.IsSameFaction(zdo, Player.m_localPlayer)) continue;
                        var pos = zdo.GetPosition();
                        var name = zdo.GetString(Constants.Z_GivenName);

                        if (!m_mobPins.ContainsKey(zid))
                        {
                            var pin = Minimap.instance.AddPin(pos, Minimap.PinType.Icon3, name, false, false);
                            m_mobPins.Add(zid, pin);
                        }
                        else
                        {
                            m_mobPins[zid].m_pos = pos;
                            m_mobPins[zid].m_name = name;
                        }
                    }
                    var idsToRemove = m_mobPins.Where(m => !m_allMobZDOIDs.Any(z => z == m.Key)).Select(m => (m.Key, m.Value)).ToArray();
                    foreach (var pin in idsToRemove)
                    {
                        m_mobPins.Remove(pin.Key);
                        Minimap.instance.RemovePin(pin.Value);
                        Minimap.instance.AddPin(pin.Value.m_pos, Minimap.PinType.Death, pin.Value.m_name, true, false);
                    }
                }
                catch (System.Exception e)
                {
                    Logger.LogError(e.Message);
                }
            }
        }
    }
}