using System;
using System.Collections.Generic;

namespace RagnarsRokare.Factions
{
    internal static class FactionManager
    {
        private static Dictionary<string,Faction> Factions = new Dictionary<string,Faction>();

        static FactionManager()
        {
            AddFaction(new Faction { FactionId = DefaultNPCFactionId, Name = "Default", Description = "NPC default faction" });
            AddFaction(new Faction { FactionId = "9A026272-CFC9-4703-98A8-F5C667841CB0", Name = "Test", Description = "Test faction" });
        }

        public const string DefaultNPCFactionId = "7BF96D8F-5B46-4EBF-9027-51318E55B885";

        public static Faction GetFaction(string factionId)
        {
            return Factions.TryGetValue(factionId, out Faction result) ? result : null;
        }

        public static void AddFaction(Faction faction)
        {
            if (faction == null) return;
            if (Factions.ContainsKey(faction.FactionId)) return;
            Factions.Add(faction.FactionId, faction);
        }

        public static void UpdateFaction(Faction faction)
        {
            if (Factions.TryGetValue(faction.FactionId, out Faction result))
            {
                result.Name = faction.Name;
                result.Description = faction.Description;
            }
        }

        // TODO: Load factions

        // TODO: Save factions
    }
}
