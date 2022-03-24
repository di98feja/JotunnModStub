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
            IEnumerable<(string, float)> standings = GetStandings(zdo.GetString(Misc.Constants.Z_FactionStandings));
            if (standings.Any(s => s.Item1 == factionId))
            {
                return standings.Single(s => s.Item1 == factionId.ToString()).Item2;
            }
            else
            {
                return Misc.Constants.NeutralStanding;
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

            Mathf.Clamp(standing, Misc.Constants.MinStanding, Misc.Constants.MaxStanding);
            var allStandings = GetStandings(zdo.GetString(Misc.Constants.Z_FactionStandings));
            var factionStanding = allStandings.SingleOrDefault(s => s.Item1 == factionId.ToString());
            if (factionStanding == default)
            {
                allStandings.Append<(string, float)>((factionId.ToString(), standing));
            }
            else
            {
                factionStanding.Item2 = standing;
            }
            zdo.Set(Misc.Constants.Z_FactionStandings, string.Join("|", allStandings.Select(s => $"{s.Item1};{s.Item2.ToString(CultureInfo.InvariantCulture)}")));
        }

        internal static IEnumerable<(string, float)> GetStandings(string standingsString)
        {
            foreach (System.Text.RegularExpressions.Match faction in System.Text.RegularExpressions.Regex.Matches(standingsString, RX_FactionStanding))
            {
                yield return (faction.Groups["id"].Value, float.Parse(faction.Groups["standing"].Value, CultureInfo.InvariantCulture));
            }
            
        }

    }
}
