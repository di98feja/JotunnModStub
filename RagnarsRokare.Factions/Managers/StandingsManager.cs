using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal static class StandingsManager
    {
        private const string RX_FactionStanding = @"(?<id>\w{8}\-\w{4}\-\w{4}\-\w{4}\-\w{12});(?<standing>\d+\.?\d?)\|*";

        public static float GetStandingTowards(ZDO zdo, Faction faction)
        {
            return GetStandingTowards(zdo, faction.FactionId);
        }

        public static float GetStandingTowards(ZDO zdo, string factionId)
        {
            IEnumerable<StandingInfo> standings = GetStandings(zdo.GetString(Misc.Constants.Z_FactionStandings));
            if (standings.Any(s => s.id == factionId))
            {
                return standings.Single(s => s.id == factionId.ToString()).value;
            }
            else
            {
                return Misc.Constants.Standing_Suspicious;
            }
        }

        public static void SetStandingTowards(ZDO zdo, Faction faction, float standing)
        {
            SetStandingTowards(zdo, faction.FactionId, standing);
        }

        public static void SetStandingTowards(ZDO zdo, string factionId, float standing)
        {
            if (zdo?.IsOwner() != true) return;
            if (string.IsNullOrEmpty(factionId)) return;

            Mathf.Clamp(standing, Misc.Constants.Standing_Minimum, Misc.Constants.Standing_Max);
            var allStandings = GetStandings(zdo.GetString(Misc.Constants.Z_FactionStandings)).ToList();
            var factionStanding = allStandings.SingleOrDefault(s => s.id == factionId);
            if (factionStanding == null)
            {
                allStandings.Add(new StandingInfo(factionId, standing));
            }
            else
            {
                factionStanding.value = standing;
            }
            string newFactionString = string.Join("|", allStandings.Select(s => $"{s.id};{s.value.ToString(CultureInfo.InvariantCulture)}"));
            zdo.Set(Misc.Constants.Z_FactionStandings, string.Join("|", allStandings.Select(s => $"{s.id};{s.value.ToString(CultureInfo.InvariantCulture)}")));
        }

        internal static void IncreaseStandingTowards(ZDO npcZdo, Faction faction, float standingIncrease)
        {
            SetStandingTowards(npcZdo, faction, GetStandingTowards(npcZdo, faction) + standingIncrease);
        }

        internal static IEnumerable<StandingInfo> GetStandings(string standingsString)
        {
            foreach (System.Text.RegularExpressions.Match faction in System.Text.RegularExpressions.Regex.Matches(standingsString, RX_FactionStanding))
            {
                yield return new StandingInfo(faction.Groups["id"].Value, float.Parse(faction.Groups["standing"].Value, CultureInfo.InvariantCulture));
            }
        }

        public class StandingInfo
        {
            public StandingInfo(string id, float value)
            {
                this.id = id;
                this.value = value;
            }
            public string id;
            public float value;
        }
    }
}
